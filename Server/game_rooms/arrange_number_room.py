import random
from typing import cast, override

from game_rooms.base_game_room import BaseGameRoom
from game_define import PROTOCOL_CLIENT, PROTOCOL_SERVER, ARRANGE_NUMBER_STATE
import network
from user import User, BasePlayer


class Player(BasePlayer):
	@override
	def reset(self):
		self.numbers: list[int] = []
	
	@override
	def to_bytes(self) -> bytes:
		data = bytes()
		data += self.user.uid.to_bytes(2, byteorder="little")
		data += len(self.numbers).to_bytes(1, byteorder="little")
		for number in self.numbers:
			data += number.to_bytes(1, byteorder="little")
		return data


class ArrangeNumberRoom(BaseGameRoom):
	@override
	def _init_setting(self):
		self._max_number = 100
		self._number_group_count = 1
		self._number_per_player = 1

	@override
	def _reset_game(self):
		super()._reset_game()

		self._game_state = ARRANGE_NUMBER_STATE.WAITING
		self._current_number = 0

	@override
	def _generate_player_object(self, user: User) -> Player | None:
		if self._game_state != ARRANGE_NUMBER_STATE.WAITING:
			return None
		return Player(user)

	@override
	async def _on_start_game_process(self):
		self._game_state = ARRANGE_NUMBER_STATE.PLAYING

		# 給每個玩家分配數字並通知
		player_list: list[Player] = cast(list[Player], self._players.values())
		if self._number_group_count == 0:  # 代表無限組，每個人各自隨機生成就好
			for player in player_list:
				for _ in range(self._number_per_player):
					player.numbers.append(random.randint(1, self._max_number))
		else:  # 有限組數，隨機生成數字組
			# 先檢查數字量是否足夠
			if self._number_group_count * self._max_number < len(player_list) * self._number_per_player:
				return
			
			# 從列表中抽指定數量數字
			numbers = list(range(1, self._max_number + 1)) * self._number_group_count
			for player in player_list:
				for _ in range(self._number_per_player):
					index = random.randint(0, len(numbers) - 1)
					numbers[index], numbers[-1] = numbers[-1], numbers[index]
					player.numbers.append(numbers.pop())
		
		# 先排好玩家的數字方便之後出牌時 pop
		for player in player_list:
			player.numbers.sort(reverse=True)

	@override
	async def _on_remove_player_game_process(self, uid: int):
		del uid  # Not used.

		if self._game_state != ARRANGE_NUMBER_STATE.WAITING:
			if len(self._players) < 2:
				self._reset_game()
				await self._broadcast_end(True)
				return
			
			await self._check_left_numbers()
	
	async def _check_left_numbers(self):
		"""檢查是否還有剩餘的數字可以出牌。"""
		player_list: list[Player] = cast(list[Player], self._players.values())
		if any(len(player.numbers) > 0 for player in player_list):
			return
		
		self._reset_game()
		await self._broadcast_end(False)
	
	# user requests ===========================================================================

	async def _request_set_max_number(self, uid: int, max_number: int):
		if self._game_state != ARRANGE_NUMBER_STATE.WAITING:
			return
		if max_number < 10 or max_number > 1000 or max_number == self._max_number:
			return
		if uid not in self._players:
			return
		
		self._max_number = max_number
		print(f"房間編號 {self._room_id} 使用者 {uid} 設定最大數字為 {max_number}")

		await self._broadcast_settings()
	
	async def _request_set_number_group_count(self, uid: int, group_count: int):
		if self._game_state != ARRANGE_NUMBER_STATE.WAITING:
			return
		if group_count < 1 or group_count > 50 or group_count == self._number_group_count:
			return
		if uid not in self._players:
			return
		
		self._number_group_count = group_count
		print(f"房間編號 {self._room_id} 使用者 {uid} 設定數字組數為 {group_count}")

		await self._broadcast_settings()

	async def _request_set_number_per_player(self, uid: int, number_per_player: int):
		if self._game_state != ARRANGE_NUMBER_STATE.WAITING:
			return
		if number_per_player < 1 or number_per_player > 20 or number_per_player == self._number_per_player:
			return
		if uid not in self._players:
			return
		
		self._number_per_player = number_per_player
		print(f"房間編號 {self._room_id} 使用者 {uid} 設定每人數字數量為 {number_per_player}")

		await self._broadcast_settings()

	@override
	async def _process_room_specific_request(self, user: User, protocol: PROTOCOL_CLIENT, message: bytes):
		match protocol:
			case PROTOCOL_CLIENT.SET_MAX_NUMBER:
				max_number = int.from_bytes(message, byteorder="little")
				await self._request_set_max_number(user.uid, max_number)
			case PROTOCOL_CLIENT.SET_NUMBER_GROUP_COUNT:
				group_count = message[0]
				await self._request_set_number_group_count(user.uid, group_count)
			case PROTOCOL_CLIENT.SET_NUMBER_PER_PLAYER:
				number_per_player = message[0]
				await self._request_set_number_per_player(user.uid, number_per_player)
			case PROTOCOL_CLIENT.POSE_NUMBER:
				# TODO: 處理出牌邏輯
				pass
	
	# server messages ===========================================================================

	@override
	async def _send_init_packet(self, user: User):
		data = bytes()
		data += user.uid.to_bytes(2, byteorder="little")
		# 使用者列表
		data += len(self._user_ids).to_bytes(1, byteorder="little")
		for user_id in self._user_ids:
			target_user = self._manager.get_user(user_id)
			data += target_user.to_bytes()
		# 玩家列表
		data += len(self._players).to_bytes(1, byteorder="little")
		for player in self._players.values():
			data += player.to_bytes()
		# 遊戲設定
		data += self._max_number.to_bytes(2, byteorder="little")
		data += self._number_group_count.to_bytes(1, byteorder="little")
		data += self._number_per_player.to_bytes(1, byteorder="little")
		# 遊戲階段
		data += self._game_state.to_bytes(1, byteorder="little")
		# 當前數字
		data += self._current_number.to_bytes(1, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.INIT, data)
		try:
			await user.socket.send(packet)
		except:
			pass

	async def _broadcast_end(self, is_force = False):
		end_type = 1 if is_force else 0
		packet = network.new_packet(PROTOCOL_SERVER.END, end_type.to_bytes(1, byteorder="little"))
		await self._broadcast(packet)
	
	async def _broadcast_settings(self):
		data = bytes()
		data += self._max_number.to_bytes(2, byteorder="little")
		data += self._number_group_count.to_bytes(1, byteorder="little")
		data += self._number_per_player.to_bytes(1, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.SETTINGS, data)
		await self._broadcast(packet)
