from collections.abc import Collection
import random
from typing import cast, override

from game_rooms.base_game_room import BaseGameRoom
from game_define import PROTOCOL_CLIENT, PROTOCOL_SERVER, ARRANGE_NUMBER_STATE, GAME_TYPE
import network
from user import User, BasePlayer


class Player(BasePlayer):
	@override
	def reset(self):
		self.numbers: list[int] = []
		self.is_urgent: bool = False
	
	@override
	def to_bytes(self) -> bytes:
		data = bytes()
		data += self.user.uid.to_bytes(2, byteorder="little")
		data += len(self.numbers).to_bytes(1, byteorder="little")
		for number in self.numbers:
			data += number.to_bytes(2, byteorder="little")
		data += self.is_urgent.to_bytes(1, byteorder="little")
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
		self._last_player_uid = 0
		self._current_number = 0

	@override
	def _generate_player_object(self, user: User) -> Player | None:
		if self._game_state != ARRANGE_NUMBER_STATE.WAITING:
			return None
		return Player(user)

	@override
	async def _on_start_game_process(self) -> bool:
		if not await super()._on_start_game_process():
			return False

		# 給每個玩家分配數字並通知
		player_list: list[Player] = cast(list[Player], self._players.values())
		if self._number_group_count == 0:  # 代表無限組，每個人各自隨機生成就好
			for player in player_list:
				for _ in range(self._number_per_player):
					player.numbers.append(random.randint(1, self._max_number))
		else:  # 有限組數，隨機生成數字組
			# 先檢查數字量是否足夠
			if self._number_group_count * self._max_number < len(player_list) * self._number_per_player:
				return False
			
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
		
		self._game_state = ARRANGE_NUMBER_STATE.PLAYING
		
		# 通知所有玩家持有的數字
		for player in player_list:
			await self._send_self_numbers(player)
		await self._broadcast_all_player_numbers(self._players.keys())

		return True

	@override
	async def _on_remove_player_game_process(self, uid: int):
		del uid  # Not used.

		if self._game_state != ARRANGE_NUMBER_STATE.WAITING:
			if len(self._players) < 2:
				await self._on_game_end_process(is_force=True)
				return
			
			await self._check_left_numbers()

	async def _on_game_end_process(self, is_force: bool = False):
		await self._broadcast_all_player_numbers()
		self._reset_game()
		await self._broadcast_end(is_force)

	async def _check_left_numbers(self):
		"""檢查是否還有剩餘的數字可以出牌。"""
		player_list: list[Player] = cast(list[Player], self._players.values())
		if any(len(player.numbers) > 0 for player in player_list):
			return
		
		await self._on_game_end_process()
	
	# user requests ===========================================================================

	async def _request_set_max_number(self, uid: int, max_number: int):
		if self._game_state != ARRANGE_NUMBER_STATE.WAITING:
			return
		if self._countdown_timer:  # 倒數中不允許更改設定
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
		if self._countdown_timer:  # 倒數中不允許更改設定
			return
		if group_count < 0 or group_count > 50 or group_count == self._number_group_count:
			return
		if uid not in self._players:
			return
		
		self._number_group_count = group_count
		print(f"房間編號 {self._room_id} 使用者 {uid} 設定數字組數為 {group_count}")

		await self._broadcast_settings()

	async def _request_set_number_per_player(self, uid: int, number_per_player: int):
		if self._game_state != ARRANGE_NUMBER_STATE.WAITING:
			return
		if self._countdown_timer:  # 倒數中不允許更改設定
			return
		if number_per_player < 1 or number_per_player > 20 or number_per_player == self._number_per_player:
			return
		if uid not in self._players:
			return
		
		self._number_per_player = number_per_player
		print(f"房間編號 {self._room_id} 使用者 {uid} 設定每人數字數量為 {number_per_player}")

		await self._broadcast_settings()

	async def _request_pose_number(self, uid: int):
		if self._game_state != ARRANGE_NUMBER_STATE.PLAYING:
			return
		if uid not in self._players:
			return
		
		player: Player = self._players[uid]
		if not player.numbers:
			return
		
		self._last_player_uid = uid
		self._current_number = player.numbers.pop()
		await self._broadcast_pose_number()

		# 檢查是不是最小數字的玩家
		player_list: list[Player] = cast(list[Player], self._players.values())
		if any(p.numbers[-1] < self._current_number for p in player_list if p.numbers):  # 有人數字更小，爆了
			await self._on_game_end_process()
			return
		
		if not player.numbers:  # 如果出完了所有數字，檢查是否還有其他玩家有剩
			if player.is_urgent:  # 出完數字就沒什麼好急的了
				player.is_urgent = False
				await self._boardcast_urgent_players(uid, False)
			await self._send_all_player_numbers(player.user)  # 猜完的玩家可以看所有玩家的數字狀況
			await self._check_left_numbers()
	
	async def _request_set_urgent(self, uid: int, is_urgent: bool):
		if self._game_state != ARRANGE_NUMBER_STATE.PLAYING:
			return
		if uid not in self._players:
			return
		
		player: Player = self._players[uid]
		if player.is_urgent == is_urgent:
			return
		if not player.numbers:  # 沒有數字可以出了就沒有急不急的問題
			return
		
		player.is_urgent = is_urgent
		
		await self._boardcast_urgent_players(uid, is_urgent)

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
				await self._request_pose_number(user.uid)
			case PROTOCOL_CLIENT.SET_URGENT:
				is_urgent = message[0] != 0
				await self._request_set_urgent(user.uid, is_urgent)
	
	# server messages ===========================================================================

	@override
	async def _send_init_packet(self, user: User):
		data = bytes()
		# 房間的遊戲類型
		data += GAME_TYPE.ARRANGE_NUMBER.to_bytes(1, byteorder="little")

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
		data += self._last_player_uid.to_bytes(2, byteorder="little")
		data += self._current_number.to_bytes(2, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.INIT, data)
		try:
			await user.socket.send(packet)
		except:
			pass
	
	async def _send_self_numbers(self, player: Player):
		data = bytes()
		data += (0).to_bytes(1, byteorder="little")  # 0 代表更新玩家自身，1 代表更新所有玩家
		data += len(player.numbers).to_bytes(1, byteorder="little")
		for number in player.numbers:
			data += number.to_bytes(2, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.PLAYER_NUMBERS, data)
		try:
			await player.user.socket.send(packet)
		except:
			pass

	def _get_all_player_numbers_packet(self) -> bytes:
		player_list: list[Player] = cast(list[Player], self._players.values())

		data = bytes()
		data += len(player_list).to_bytes(1, byteorder="little")
		for player in player_list:
			data += player.user.uid.to_bytes(2, byteorder="little")
			data += len(player.numbers).to_bytes(1, byteorder="little")
			for number in player.numbers:
				data += number.to_bytes(2, byteorder="little")
		return data

	async def _send_all_player_numbers(self, user: User):
		data = bytes()
		data += (1).to_bytes(1, byteorder="little")  # 0 代表更新玩家自身，1 代表更新所有玩家
		data += self._get_all_player_numbers_packet()
		
		packet = network.new_packet(PROTOCOL_SERVER.PLAYER_NUMBERS, data)
		try:
			await user.socket.send(packet)
		except:
			pass

	async def _broadcast_all_player_numbers(self, exclude_clients: Collection[int] = {}):
		data = bytes()
		data += (1).to_bytes(1, byteorder="little")  # 0 代表更新玩家自身，1 代表更新所有玩家
		data += self._get_all_player_numbers_packet()
		
		packet = network.new_packet(PROTOCOL_SERVER.PLAYER_NUMBERS, data)
		await self._broadcast(packet, exclude_clients)

	async def _broadcast_settings(self):
		data = bytes()
		data += self._max_number.to_bytes(2, byteorder="little")
		data += self._number_group_count.to_bytes(1, byteorder="little")
		data += self._number_per_player.to_bytes(1, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.SETTINGS, data)
		await self._broadcast(packet)
	
	async def _broadcast_pose_number(self):
		data = bytes()
		data += self._last_player_uid.to_bytes(2, byteorder="little")
		data += self._current_number.to_bytes(2, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.POSE_NUMBER, data)
		await self._broadcast(packet)

	async def _boardcast_urgent_players(self, uid: int, is_urgent: bool):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		data += (1 if is_urgent else 0).to_bytes(1, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.URGENT_PLAYER, data)
		await self._broadcast(packet)

	async def _broadcast_end(self, is_force: bool = False):
		end_type = 1 if is_force else 0
		packet = network.new_packet(PROTOCOL_SERVER.END, end_type.to_bytes(1, byteorder="little"))
		await self._broadcast(packet)
