import traceback
from typing import override
import websockets

from game_define import PROTOCOL_CLIENT, PROTOCOL_SERVER, GAME_TYPE, CONST
from game_rooms.base_game_room import BaseGameRoom
from game_rooms.guess_word_room import GuessWordRoom
from game_rooms.arrange_number_room import ArrangeNumberRoom
from managers.user_manger_inteface import IUserManager
import network
from user import User


class GameManager(IUserManager):
	def __init__(self):
		self._users: dict[int, User] = {}
		self._rooms: dict[int, BaseGameRoom] = {}
	
	async def _send_uid(self, user: User):
		"""發送使用者 ID 給使用者。"""
		packet = network.new_packet(PROTOCOL_SERVER.UID, user.uid.to_bytes(2, byteorder="little"))
		try:
			await user.socket.send(packet)
		except:
			pass
	
	async def _send_version_check_result(self, user: User):
		"""發送遊戲版本給使用者。"""
		packet = network.new_packet(PROTOCOL_SERVER.VERSION, CONST.GAME_VERSION.to_bytes(4, byteorder="little"))
		try:
			await user.socket.send(packet)
		except:
			pass
	
	async def _send_room_id(self, user: User, id: int):
		"""發送房間 ID 給使用者。

		正數時為進入的房間 ID
		創建失敗為 -1
		加入不存在房間為 -2
		"""
		packet = network.new_packet(PROTOCOL_SERVER.ROOM_ID, id.to_bytes(4, signed=True, byteorder="little"))
		try:
			await user.socket.send(packet)
		except:
			pass
	
	async def handle_client_new(self, websocket: websockets.ServerConnection):
		"""處理單一客戶端的連線 (新版 websockets API 使用)。"""
		await self.handle_client(websocket, None)
	
	async def handle_client(self, websocket: websockets.ServerConnection, path: str):
		"""處理單一客戶端的連線。"""
		del path  # unused parameter
		
		try:
			user = User.create(websocket)
			if user == None:
				print(f"同時連線數超過上限，中斷來自 {websocket.remote_address} 的連線")
				return
			
			self.add_user(user)
			print(f"新連線：{user.uid} ({websocket.remote_address})")
			await self._send_uid(user)
			
			async for message in websocket:
				protocol = message[0]
				if await self._process_message_check_should_close(user, protocol, message[1:]):
					break
		except websockets.exceptions.ConnectionClosedOK:
			pass
		except Exception as e:
			print(f"處理 {user.uid} ({websocket.remote_address}) 的連線時發生錯誤：{e}")
			print(traceback.format_exc())
		finally:
			print(f"{user.uid} ({websocket.remote_address}) 連線中斷")
			await self.remove_user(user)

	async def _process_message_check_should_close(self, user: User, protocol: PROTOCOL_CLIENT, message: bytes) -> bool:
		"""處理來自客戶端的訊息。
		
		如果需要關閉連線則返回 True
		"""
		print(f"Received protocol {protocol} from user {user.uid}, size: {len(message)} bytes")
		match protocol:
			case PROTOCOL_CLIENT.VERSION:
				if user.version_checked:
					return False
				
				version = int.from_bytes(message, byteorder="little")
				await self._send_version_check_result(user)
				if version != CONST.GAME_VERSION:
					await user.socket.close()
					return True
				
				user.version_checked = True
			case PROTOCOL_CLIENT.NAME:
				if not await user.check_version():
					return True
				if len(message) > 255:
					return False
				
				new_name = message.decode("utf8").strip()
				if new_name == user.name:
					return False
				
				if '(' in new_name or ')' in new_name:
					return False
				
				user.name = new_name
				print(f"使用者 {user.uid} 設定名稱為 {new_name}")
				
				if user.room_id >= 0:
					room = self._rooms.get(room_id)
					if room:
						await room.broadcast_rename(user.uid, new_name)
			case PROTOCOL_CLIENT.CREATE_ROOM:
				if not await user.check_version():
					return True
				if user.room_id >= 0:
					return False
				
				game_type = int.from_bytes(message, byteorder="little")
				match game_type:
					case GAME_TYPE.GUESS_WORD:
						room = GuessWordRoom.create(self)
					case GAME_TYPE.ARRANGE_NUMBER:
						room = ArrangeNumberRoom.create(self)
					case _:
						room = None
				
				if room == None:
					await self._send_room_id(user, -1)
					return False
				
				room_id = room.get_id()
				user.room_id = room_id
				self._rooms[room_id] = room
				await room.add_user(user)
				
				await self._send_room_id(user, room_id)
				print(f"使用者 {user.uid} 建立房間編號 {room_id}")
			case PROTOCOL_CLIENT.JOIN_ROOM:
				if not await user.check_version():
					return True
				if user.room_id >= 0:
					return False
				
				room_id = int.from_bytes(message, byteorder="little")
				room = self._rooms.get(room_id)
				if room == None:
					await self._send_room_id(user, -2)
					return False
				
				user.room_id = room_id
				await room.add_user(user)
				
				await self._send_room_id(user, room_id)
				print(f"使用者 {user.uid} 加入房間編號 {room_id}")
			case PROTOCOL_CLIENT.LEAVE_ROOM:
				if user.room_id < 0:
					return False
				
				await self._user_leave_room(user)
				user.room_id = -1
			case _:
				# 處理房間內操作的請求
				if user.room_id < 0:
					return False
				
				room = self._rooms[user.room_id]
				await room.process_request(user, protocol, message)
	
	@override
	def get_user(self, uid: int) -> User | None:
		return self._users.get(uid)

	@override
	def add_user(self, user: User):
		"""新增使用者到管理器中。"""
		self._users[user.uid] = user
	
	async def _user_leave_room(self, user: User):
		"""處理使用者離開房間。"""
		room = self._rooms.get(user.room_id)
		if room:
			await room.remove_user(user.uid)
			print(f"玩家 {user.uid} 已離開房間 {user.room_id}")
			if room.is_empty():
				del self._rooms[user.room_id]
				print(f"已移除空房間 {user.room_id}")

	@override
	async def remove_user(self, user: User):
		if user.room_id >= 0:
			await self._user_leave_room(user)
		del self._users[user.uid]
		print(f"玩家 {user.name} ({user.uid}) 已移除")
