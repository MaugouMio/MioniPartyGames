import asyncio
import abc
from collections.abc import Collection

from game_define import PROTOCOL_CLIENT, PROTOCOL_SERVER, CONST
from managers.user_manger_inteface import IUserManager
import id_generator
import network
from user import User, BasePlayer


class BaseGameRoom(abc.ABC):
	def __init__(self, id: int, user_manager: IUserManager):
		self._room_id = id
		self._manager = user_manager
		self._is_playing = False
		
		self._user_ids: set[int] = set()
		self._players: dict[int, BasePlayer] = dict()
		self._countdown_timer: asyncio.Task = None

		self._init_setting()
		self._reset_game()
	
	def _init_setting(self):
		"""遊戲房間初始設定。"""

	@classmethod
	def create(cls, user_manager: IUserManager):
		"""創建遊戲房間。"""
		room_id = id_generator.generate_room_id()
		if room_id < 0:
			return None
		return cls(room_id, user_manager)
	
	def get_id(self):
		"""取得房間編號。"""
		return self._room_id
	
	def is_empty(self):
		"""檢查是否為空房間。"""
		return not self._user_ids
	
	async def add_user(self, user: User):
		"""使用者進入房間。"""
		if user.uid in self._user_ids:
			return
		
		self._user_ids.add(user.uid)
		
		await self._send_init_packet(user)
		await self._broadcast_connect(user.uid, user.name)
	
	async def remove_user(self, uid: int):
		"""使用者離開房間。"""
		if uid not in self._user_ids:
			return
		
		self._user_ids.remove(uid)
		await self._remove_player(uid)
		
		await self._broadcast_disconnect(uid)
	
	@abc.abstractmethod
	def _generate_player_object(self, user: User) -> BasePlayer | None:
		"""生成玩家物件。
		
		如果遊戲狀態不允許加入玩家，則返回 None。
		"""
	
	async def _add_player(self, user: User):
		"""添加玩家到遊戲中。"""
		if user.uid in self._players:
			return
		
		player = self._generate_player_object(user)
		if not player:
			return
		
		self._players[user.uid] = player
		print(f"房間編號 {self._room_id} 使用者 {user.uid} 加入遊戲")
		
		await self._stop_countdown()
		await self._broadcast_join(user.uid)
	
	@abc.abstractmethod
	async def _on_remove_player_game_process(self, uid: int):
		"""當玩家離開遊戲時的遊戲機制處理。"""
	
	async def _remove_player(self, uid: int):
		"""從遊戲中移除玩家。"""
		if uid not in self._players:
			return
		
		await self._stop_countdown()
		del self._players[uid]
		print(f"房間編號 {self._room_id} 使用者 {uid} 退出遊戲")

		await self._on_remove_player_game_process(uid)
		await self._broadcast_leave(uid)
	
	# user requests ===========================================================================

	async def _request_start(self, uid):
		if self._is_playing:
			return
		if uid not in self._players:
			return
		if len(self._players) < 2:
			return
		
		await self._start_countdown()
		print(f"房間編號 {self._room_id} 使用者 {uid} 要求開始遊戲")

	async def _request_cancel_start(self, uid):
		if self._is_playing:
			return
		if uid not in self._players:
			return
		
		await self._stop_countdown()
		print(f"房間編號 {self._room_id} 使用者 {uid} 取消開始遊戲倒數")
	
	async def process_request(self, user: User, protocol: PROTOCOL_CLIENT, message: bytes):
		"""處理使用者的房間相關操作請求。"""
		match protocol:
			case PROTOCOL_CLIENT.JOIN_GAME:
				await self._add_player(user)
			case PROTOCOL_CLIENT.LEAVE_GAME:
				await self._remove_player(user.uid)
			case PROTOCOL_CLIENT.START:
				await self._request_start(user.uid)
			case PROTOCOL_CLIENT.CANCEL_START:
				await self._request_cancel_start(user.uid)
			case PROTOCOL_CLIENT.CHAT:
				if len(message) > 257:  # 255 for name + 2 for hide_uid
					return
				
				hide_uid = int.from_bytes(message[:2], byteorder="little")
				await self._broadcast_chat(user.uid, message[2:], {hide_uid} if hide_uid > 0 else {})
			case _:
				# 處理不同類型房間特定的請求
				await self._process_room_specific_request(user, protocol, message)
	
	@abc.abstractmethod
	async def _process_room_specific_request(self, user: User, protocol: PROTOCOL_CLIENT, message: bytes):
		"""處理非房間共用機制的客戶端請求。"""
	
	# game flows ===========================================================================
	
	async def _countdown_async(self):
		"""倒數開始計時器。"""
		await asyncio.sleep(CONST.START_COUNTDOWN_DURATION)
		await self._start_game()

	async def _start_countdown(self):
		"""開始遊戲倒數計時。"""
		if self._countdown_timer:
			return
		
		self._countdown_timer = asyncio.create_task(self._countdown_async())
		await self._broadcast_start_countdown()
	
	async def _stop_countdown(self):
		"""停止遊戲倒數計時。"""
		if not self._countdown_timer:
			return
		
		self._countdown_timer.cancel()
		self._countdown_timer = None
		await self._broadcast_start_countdown(is_stop=True)
	
	@abc.abstractmethod
	async def _on_start_game_process(self) -> bool:
		"""當遊戲開始時的遊戲機制處理。"""
		await self._boradcast_reset_game_data()
		return True

	async def _start_game(self):
		"""開始遊戲。"""
		self._countdown_timer = None
		
		self._reset_game()
		if not await self._on_start_game_process():
			return

		self._is_playing = True
		await self._broadcast_start()
	
	@abc.abstractmethod
	def _reset_game(self):
		"""重置遊戲狀態。
		
		這個方法在遊戲開始前和遊戲結束後都會被調用。
		override 時需要呼叫 `super()._reset_game()` 來確保基礎狀態被重置。
		"""
		self._is_playing = False
		for player in self._players.values():
			player.reset()
	
	# server messages ===========================================================================

	@abc.abstractmethod
	async def _send_init_packet(self, user: User):
		"""發送初始化封包給新進房間的使用者。"""

	async def _broadcast(self, packet: bytes, exclude_clients: Collection[int] = {}):
		"""廣播訊息給所有房間內的使用者。"""
		disconnected_users = []
		for uid in self._user_ids:
			if uid not in exclude_clients:
				user = self._manager.get_user(uid)
				try:
					await user.socket.send(packet)
				except:
					disconnected_users.append(user)
		
		for user in disconnected_users:
			await self._manager.remove_user(user)
	
	async def _broadcast_connect(self, uid: int, name: str):
		"""廣播使用者進入房間。"""
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		encoded_name = name.encode("utf8")
		data += len(encoded_name).to_bytes(1, byteorder="little")
		data += encoded_name
		
		packet = network.new_packet(PROTOCOL_SERVER.CONNECT, data)
		await self._broadcast(packet, {uid})
	
	async def _broadcast_disconnect(self, uid: int):
		"""廣播使用者離開房間。"""
		packet = network.new_packet(PROTOCOL_SERVER.DISCONNECT, uid.to_bytes(2, byteorder="little"))
		await self._broadcast(packet)
	
	async def broadcast_rename(self, uid: int, name: str):
		"""廣播使用者更名。"""
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		encoded_name = name.encode("utf8")
		data += len(encoded_name).to_bytes(1, byteorder="little")
		data += encoded_name
		
		packet = network.new_packet(PROTOCOL_SERVER.NAME, data)
		await self._broadcast(packet)
	
	async def _broadcast_join(self, uid: int):
		"""廣播使用者加入遊戲。"""
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.JOIN_GAME, data)
		await self._broadcast(packet)
	
	async def _broadcast_leave(self, uid: int):
		"""廣播使用者離開遊戲。"""
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.LEAVE_GAME, data)
		await self._broadcast(packet)
	
	async def _broadcast_start_countdown(self, is_stop: bool = False):
		"""廣播遊戲開始倒數計時。"""
		data = bytes()
		if is_stop:
			data += int(0).to_bytes(1, byteorder="little")
		else:
			data += int(1).to_bytes(1, byteorder="little")
			data += CONST.START_COUNTDOWN_DURATION.to_bytes(1, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.START_COUNTDOWN, data)
		await self._broadcast(packet)

	async def _boradcast_reset_game_data(self):
		"""廣播重置遊戲資料。"""
		packet = network.new_packet(PROTOCOL_SERVER.RESET_GAME_DATA, bytes())
		await self._broadcast(packet)
	
	async def _broadcast_start(self):
		"""廣播遊戲開始。"""
		packet = network.new_packet(PROTOCOL_SERVER.START, bytes())
		await self._broadcast(packet)
	
	async def _broadcast_chat(self, uid: int, encoded_message: bytes, hide_uids: Collection[int]):
		"""廣播使用者聊天訊息。"""
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		
		data += len(encoded_message).to_bytes(1, byteorder="little")
		data += encoded_message
		
		data += (1 if hide_uids else 0).to_bytes(1, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.CHAT, data)
		await self._broadcast(packet, hide_uids)
