import random
from typing import cast, override

from game_rooms.base_game_room import BaseGameRoom
from game_define import PROTOCOL_CLIENT, PROTOCOL_SERVER, GUESS_WORD_STATE, GAME_TYPE
import network
from user import User, BasePlayer


class Player(BasePlayer):
	@override
	def reset(self):
		self.question = ""
		self.question_locked = False
		self.guess_history: list[tuple[str, int]] = []
		self.success_round = 0
		self.skipped_round = 0
	
	@override
	def to_bytes(self) -> bytes:
		data = bytes()
		data += self.user.uid.to_bytes(2, byteorder="little")
		encoded_question = self.question.encode("utf8")
		data += len(encoded_question).to_bytes(1, byteorder="little")
		data += encoded_question
		data += len(self.guess_history).to_bytes(1, byteorder="little")
		for guess in self.guess_history:
			encoded_guess = guess[0].encode("utf8")
			data += len(encoded_guess).to_bytes(1, byteorder="little")
			data += encoded_guess
			data += guess[1].to_bytes(1, byteorder="little")
		data += self.success_round.to_bytes(2, signed=True, byteorder="little")
		return data


class GuessWordRoom(BaseGameRoom):
	@override
	def _reset_game(self):
		super()._reset_game()

		self._game_state = GUESS_WORD_STATE.WAITING
		self._current_round = 0
		self._player_order: list[int] = []
		self._current_guessing_idx = 0
		self._votes: dict[int, int] = {}
		
		self.temp_guess = ""
	
	@override
	def _generate_player_object(self, user: User) -> Player | None:
		if self._game_state != GUESS_WORD_STATE.WAITING:
			return None
		return Player(user)

	@override
	async def _on_start_game_process(self) -> bool:
		self._current_round = 1
		self._game_state = GUESS_WORD_STATE.PREPARING
		
		self._player_order = list(self._players.keys())
		random.shuffle(self._player_order)
		self._current_guessing_idx = 0
		
		await self._broadcast_player_order(include_list=True)
		return True

	@override
	async def _on_remove_player_game_process(self, uid: int):
		if self._game_state != GUESS_WORD_STATE.WAITING:
			if len(self._players) < 2:
				self._reset_game()
				await self._broadcast_end(True)
				return
			
			order_index = self._player_order.index(uid)
			del self._player_order[order_index]
			if order_index <= self._current_guessing_idx:
				self._current_guessing_idx -= 1  # 後面要往回遞補
				if order_index > self._current_guessing_idx:  # 當前猜題者離開，往後順延
					await self._advance_to_next_player()
				else:
					await self._broadcast_player_order()
		
		if uid in self._votes:
			del self._votes[uid]
		
		await self._check_all_given_words()
		await self._check_all_votes()

	async def _check_all_given_words(self):
		"""檢查是否所有玩家都已出題。"""
		if self._game_state != GUESS_WORD_STATE.PREPARING:
			return
		
		players: list[Player] = cast(list[Player], self._players.values())
		if any(player.question_locked == False for player in players):
			return
		
		self._game_state = GUESS_WORD_STATE.GUESSING
		await self._broadcast_game_state()

	async def _check_all_votes(self):
		"""檢查是否所有玩家都已投票。"""
		if self._game_state != GUESS_WORD_STATE.VOTING:
			return
		if len(self._votes) < len(self._players) - 1:
			return
		
		yes_votes = 0
		no_votes = 0
		abstain_votes = 0
		for vote in self._votes.values():
			if vote == 1:
				yes_votes += 1
			elif vote == 2:
				no_votes += 1
			else:
				abstain_votes += 1
		
		if yes_votes == no_votes:
			self._current_guessing_idx -= 1  # 無效投票，讓玩家再猜一個類型
			await self._broadcast_guess_again()
		else:
			guessing_player_uid = self._player_order[self._current_guessing_idx]
			result = 1 if yes_votes > no_votes else 0
			
			player: Player = self._players[guessing_player_uid]
			player.guess_history.append((self.temp_guess, result))
			await self._broadcast_guess_record(guessing_player_uid, self.temp_guess, result)

		await self._advance_to_next_player()

	async def _advance_to_next_player(self):
		"""移動到下一個需要猜測的玩家。"""
		self.temp_guess = ""
		
		for i in range(len(self._player_order)):
			self._current_guessing_idx += 1
			if self._current_guessing_idx >= len(self._player_order):
				self._current_round += 1
				self._current_guessing_idx = 0
			
			next_uid = self._player_order[self._current_guessing_idx]
			# 跳過已經猜出的玩家
			player: Player = self._players[next_uid]
			if player.success_round != 0:
				continue
			
			self._game_state = GUESS_WORD_STATE.GUESSING
			
			await self._broadcast_player_order()
			await self._broadcast_game_state()
			return
		
		# 所有人都猜出來了
		self._reset_game()
		await self._broadcast_end()
	
	# user requests ===========================================================================

	async def _request_assign_question(self, uid: int, word: str, is_locked: bool):
		if self._game_state != GUESS_WORD_STATE.PREPARING:
			return
		if uid not in self._players:
			return
		
		next_player: Player = self._players[self._player_order[(self._player_order.index(uid) + 1) % len(self._player_order)]]
		# if next_player.question != "":
			# return
		
		if word == "" or (word == next_player.question and is_locked == next_player.question_locked):
			return
		
		next_player.question = word
		next_player.question_locked = is_locked
		if is_locked:
			print(f"房間編號 {self._room_id} 使用者 {uid} 向 {next_player.user.uid} 出題：{word}")
		else:
			print(f"房間編號 {self._room_id} 使用者 {uid} 展示 {next_player.user.uid} 的題目：{word}")
		await self._broadcast_question(next_player)
		
		await self._check_all_given_words()
	
	async def _request_guess(self, uid: int, guess: str):
		if self._game_state != GUESS_WORD_STATE.GUESSING:
			return
		if uid != self._player_order[self._current_guessing_idx]:
			return
		
		player: Player = self._players[uid]
		
		# 表示跳過
		if not guess:
			player.skipped_round += 1
			await self._broadcast_skip_guess(uid)
			await self._advance_to_next_player()
			return
		
		if guess.lower() == player.question.lower():
			player.success_round = self._current_round - player.skipped_round
			await self._broadcast_success(uid, player.success_round, guess)
			await self._advance_to_next_player()
			return
		
		self.temp_guess = guess
		self._votes.clear()
		self._game_state = GUESS_WORD_STATE.VOTING
		print(f"房間編號 {self._room_id} 使用者 {uid} 猜題：{guess}")
		
		await self._broadcast_guess()
	
	async def _request_vote(self, uid: int, vote: int):
		if self._game_state != GUESS_WORD_STATE.VOTING:
			return
		if vote < 0 or vote > 2:
			return
		if uid not in self._players:
			return
		if uid == self._player_order[self._current_guessing_idx]:
			return
		
		self._votes[uid] = vote
		print(f"房間編號 {self._room_id} 使用者 {uid} 進行投票：{vote}")
		await self._broadcast_vote(uid, vote)
		await self._check_all_votes()
	
	async def _request_give_up(self, uid: int):
		if self._game_state != GUESS_WORD_STATE.GUESSING:
			return
		if uid != self._player_order[self._current_guessing_idx]:
			return
		
		player: Player = self._players[uid]
		player.success_round = -1
		await self._broadcast_success(uid, -1, player.question)
		await self._advance_to_next_player()
	
	@override
	async def _process_room_specific_request(self, user: User, protocol: PROTOCOL_CLIENT, message: bytes):
		match protocol:
			case PROTOCOL_CLIENT.QUESTION:
				if len(message) > 256:
					return
				
				is_locked = message[0] == 1
				word = message[1:].decode("utf8").strip()
				await self._request_assign_question(user.uid, word, is_locked)
			case PROTOCOL_CLIENT.GUESS:
				if len(message) > 255:
					return
				
				guess = message.decode("utf8").strip()
				await self._request_guess(user.uid, guess)
			case PROTOCOL_CLIENT.VOTE:
				vote = int.from_bytes(message, byteorder="little")
				await self._request_vote(user.uid, vote)
			case PROTOCOL_CLIENT.GIVE_UP:
				await self._request_give_up(user.uid)
	
	# server messages ===========================================================================

	@override
	async def _send_init_packet(self, user: User):
		data = bytes()
		# 房間的遊戲類型
		data += GAME_TYPE.GUESS_WORD.to_bytes(1, byteorder="little")

		# 使用者列表
		data += len(self._user_ids).to_bytes(1, byteorder="little")
		for user_id in self._user_ids:
			target_user = self._manager.get_user(user_id)
			data += target_user.to_bytes()
		# 玩家列表
		data += len(self._players).to_bytes(1, byteorder="little")
		for player in self._players.values():
			data += player.to_bytes()
		# 遊戲階段
		data += self._game_state.to_bytes(1, byteorder="little")
		# 玩家順序
		data += len(self._player_order).to_bytes(1, byteorder="little")
		for player_uid in self._player_order:
			data += player_uid.to_bytes(2, byteorder="little")
		data += self._current_guessing_idx.to_bytes(1, byteorder="little")
		# 投票狀況
		encoded_guess = self.temp_guess.encode("utf8")
		data += len(encoded_guess).to_bytes(1, byteorder="little")
		data += encoded_guess
		
		data += len(self._votes).to_bytes(1, byteorder="little")
		for vote_uid, vote in self._votes.items():
			data += vote_uid.to_bytes(2, byteorder="little")
			data += vote.to_bytes(1, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.INIT, data)
		try:
			await user.socket.send(packet)
		except:
			pass

	async def _broadcast_game_state(self):
		packet = network.new_packet(PROTOCOL_SERVER.GAMESTATE, self._game_state.to_bytes(1, byteorder="little"))
		await self._broadcast(packet)
	
	async def _broadcast_player_order(self, include_list: bool = False):
		data = bytes()
		data += self._current_guessing_idx.to_bytes(1, byteorder="little")
		if include_list:
			data += int(1).to_bytes(1, byteorder="little")
			data += len(self._player_order).to_bytes(1, byteorder="little")
			for uid in self._player_order:
				data += uid.to_bytes(2, byteorder="little")
		else:
			data += int(0).to_bytes(1, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.PLAYER_ORDER, data)
		await self._broadcast(packet)
	
	async def _broadcast_question(self, player: Player):
		data = bytes()
		data += player.user.uid.to_bytes(2, byteorder="little")
		data += (1 if player.question_locked else 0).to_bytes(1, byteorder="little")
		encoded_question = player.question.encode("utf8")
		data += len(encoded_question).to_bytes(1, byteorder="little")
		data += encoded_question
		
		packet = network.new_packet(PROTOCOL_SERVER.QUESTION, data)
		await self._broadcast(packet, {player.user.uid})
		
		# 傳給玩家本身的資訊不含題目，只做提示已經出好題了
		data = bytes()
		data += player.user.uid.to_bytes(2, byteorder="little")
		data += (1 if player.question_locked else 0).to_bytes(1, byteorder="little")
		packet = network.new_packet(PROTOCOL_SERVER.QUESTION, data)
		try:
			await player.user.socket.send(packet)
		except:
			pass
	
	async def _broadcast_success(self, uid: int, success_round: int, answer: str):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		data += success_round.to_bytes(2, signed=True, byteorder="little")
		
		encoded_answer = answer.encode("utf8")
		data += len(encoded_answer).to_bytes(1, byteorder="little")
		data += encoded_answer
		
		packet = network.new_packet(PROTOCOL_SERVER.SUCCESS, data)
		await self._broadcast(packet)
	
	async def _broadcast_guess(self):
		data = bytes()
		encoded_guess = self.temp_guess.encode("utf8")
		data += len(encoded_guess).to_bytes(1, byteorder="little")
		data += encoded_guess
		
		packet = network.new_packet(PROTOCOL_SERVER.GUESS, data)
		await self._broadcast(packet)
	
	async def _broadcast_vote(self, uid: int, vote: int):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		data += vote.to_bytes(1, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.VOTE, data)
		await self._broadcast(packet)
	
	async def _broadcast_guess_again(self):
		packet = network.new_packet(PROTOCOL_SERVER.GUESS_AGAIN, bytes())
		await self._broadcast(packet)
	
	async def _broadcast_guess_record(self, uid: int, guess: str, result: int):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		
		encoded_guess = guess.encode("utf8")
		data += len(encoded_guess).to_bytes(1, byteorder="little")
		data += encoded_guess
		
		data += result.to_bytes(1, byteorder="little")
		
		packet = network.new_packet(PROTOCOL_SERVER.GUESS_RECORD, data)
		await self._broadcast(packet)
	
	async def _broadcast_skip_guess(self, uid: int):
		packet = network.new_packet(PROTOCOL_SERVER.SKIP_GUESS, uid.to_bytes(2, byteorder="little"))
		await self._broadcast(packet)
	
	async def _broadcast_end(self, is_force: bool = False):
		end_type = 1 if is_force else 0
		packet = network.new_packet(PROTOCOL_SERVER.END, end_type.to_bytes(1, byteorder="little"))
		await self._broadcast(packet)
