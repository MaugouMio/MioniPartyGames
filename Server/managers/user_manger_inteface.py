import abc

from user import User


class IUserManager:
	"""使用者管理介面，定義了使用者相關的操作。"""

	@abc.abstractmethod
	def get_user(self, uid: int) -> User | None:
		"""根據使用者 ID 獲取使用者實例。"""
	
	@abc.abstractmethod
	def add_user(self, user: User):
		"""新增使用者。"""
	
	@abc.abstractmethod
	async def remove_user(self, user: User):
		"""移除使用者。"""
