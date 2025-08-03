import abc
import websockets

import id_generator


class User:
	"""使用者類別，代表連線的客戶端。"""
	def __init__(self, websocket: websockets.ServerConnection, id: int):
		self.socket = websocket
		self.uid = id
		self.name = ""
		self.version_checked = False
		self.room_id = -1
	
	def __del__(self):
		id_generator.release_user_id(self.uid)
	
	@classmethod
	def create(cls, websocket: websockets.ServerConnection) -> 'User | None':
		"""創建一個新的使用者實例。
		
		如果無法分配新的 ID 則返回 None。
		"""
		uid = id_generator.generate_user_id()
		if uid < 0:
			return None
		return cls(websocket, uid)
	
	async def check_version(self) -> bool:
		if not self.version_checked:
			await self.socket.close()
			return False
		return True
	
	def to_bytes(self) -> bytes:
		"""將使用者資訊轉換為位元組格式。"""
		data = bytes()
		data += self.uid.to_bytes(2, byteorder="little")
		encoded_name = self.name.encode("utf8")
		data += len(encoded_name).to_bytes(1, byteorder="little")
		data += encoded_name
		return data


class BasePlayer(abc.ABC):
	"""遊戲玩家的基礎類別，所有玩家類別都應繼承自此類別。"""
	def __init__(self, user: User):
		self.user = user
		self.reset()
	
	@abc.abstractmethod
	def reset(self):
		"""重置玩家資料。
		
		新玩家與遊戲開始時都會調用此方法。
		"""
	
	@abc.abstractmethod
	def to_bytes(self) -> bytes:
		"""將玩家資訊轉換為位元組格式。"""
