import socket
import threading
import random
import time
import traceback

from game_define import *
import id_generator

class User:
	def __init__(self, conn, id):
		self.socket = conn
		self.uid = id
		self.name = ""
		self.version_checked = False
		self.room_id = -1
	
	def __del__(self):
		id_generator.release_user_id(self.uid)
	
	@classmethod
	def create(cls, conn):
		uid = id_generator.generate_user_id()
		if uid < 0:
			return None
		return cls(conn, uid)
	
	def check_version(self):
		if not self.version_checked:
			self.socket.close()
			return False
		return True


class Player:
	def __init__(self, user):
		self.user = user
		self.reset()
	
	def reset(self):
		self.question = ""
		self.question_locked = False
		self.guess_history = []
		self.success_round = 0
		self.skipped_round = 0


def new_packet(protocol, data):
	packet = bytes()
	packet += protocol.to_bytes(1, byteorder="little")
	packet += len(data).to_bytes(4, byteorder="little")
	packet += data
	return packet


class GameRoom:
	def __init__(self, id, game_manager):
		self._room_id = id
		self._manager = game_manager
		
		self._user_ids = set()
		self._players = dict()
		self._countdown_timer = None
		
		self._reset_game()
	
	def __del__(self):
		id_generator.release_room_id(self._room_id)
	
	@classmethod
	def create(cls, game_manager):
		room_id = id_generator.generate_room_id()
		if room_id < 0:
			return None
		return cls(room_id, game_manager)
	
	def get_id(self):
		return self._room_id
	
	def add_user(self, user):
		if user.uid in self._user_ids:
			return
		
		self._user_ids.add(user.uid)
		
		self.send_init_packet(user)
		self.broadcast_connect(user.uid, user.name)
	
	def remove_user(self, uid):
		if uid not in self._user_ids:
			return
		
		self.remove_player(uid)
		self._user_ids.remove(uid)
		
		self.broadcast_disconnect(uid)
	
	def is_empty(self):
		return not self._user_ids
	
	def add_player(self, user):
		if self._game_state != GAMESTATE.WAITING:
			return
		if user.uid in self._players:
			return
		
		player = Player(user)
		self._players[user.uid] = player
		print(f"房間編號 {self._room_id} 使用者 {user.uid} 加入遊戲")
		
		self._stop_countdown()
		self.broadcast_join(user.uid)
	
	def remove_player(self, uid):
		if uid not in self._players:
			return
		
		self._stop_countdown()
		del self._players[uid]
		print(f"房間編號 {self._room_id} 使用者 {uid} 退出遊戲")
		
		if self._game_state != GAMESTATE.WAITING:
			if len(self._players) < 2:
				self._reset_game()
				self.broadcast_leave(uid)
				self.broadcast_end(True)
				return
			
			order_index = self._player_order.index(uid)
			del self._player_order[order_index]
			if order_index <= self._current_guessing_idx:
				self._current_guessing_idx -= 1  # 後面要往回遞補
				if order_index > self._current_guessing_idx:  # 當前猜題者離開，往後順延
					self._advance_to_next_player()
				else:
					self.broadcast_player_order()
		
		if uid in self._votes:
			del self._votes[uid]
		
		self._check_all_given_words()
		self._check_all_votes()
		self.broadcast_leave(uid)

	def request_start(self, uid):
		if self._game_state != GAMESTATE.WAITING:
			return
		if uid not in self._players:
			return
		if len(self._players) < 2:
			return
		
		self._start_countdown()
		print(f"房間編號 {self._room_id} 使用者 {uid} 要求開始遊戲")

	def request_cancel_start(self, uid):
		if self._game_state != GAMESTATE.WAITING:
			return
		if uid not in self._players:
			return
		
		self._stop_countdown()
		print(f"房間編號 {self._room_id} 使用者 {uid} 取消開始遊戲倒數")
	
	def request_assign_question(self, uid, word, is_locked):
		if self._game_state != GAMESTATE.PREPARING:
			return
		if uid not in self._players:
			return
		
		next_player = self._players[self._player_order[(self._player_order.index(user.uid) + 1) % len(self._player_order)]]
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
		self.broadcast_question(next_player)
		
		self._check_all_given_words()
	
	def request_guess(self, uid, guess):
		if self._game_state != GAMESTATE.GUESSING:
			return
		if uid != self._player_order[self._current_guessing_idx]:
			return
		
		player = self._players[uid]
		
		# 表示跳過
		if not guess:
			player.skipped_round += 1
			self.broadcast_skip_guess(uid)
			self._advance_to_next_player()
			return
		
		if guess.lower() == player.question.lower():
			player.success_round = self.current_round - player.skipped_round
			self.broadcast_success(uid, player.success_round, guess)
			self._advance_to_next_player()
			return
		
		self.temp_guess = guess
		self._votes.clear()
		self._game_state = GAMESTATE.VOTING
		print(f"房間編號 {self._room_id} 使用者 {uid} 猜題：{guess}")
		
		self.broadcast_guess()
	
	def request_vote(self, uid, vote):
		if self._game_state != GAMESTATE.VOTING:
			return
		if vote < 0 or vote > 2:
			return
		if uid not in self._players:
			return
		if uid == self._player_order[self._current_guessing_idx]:
			return
		
		self._votes[uid] = vote
		print(f"房間編號 {self._room_id} 使用者 {uid} 進行投票：{vote}")
		self.broadcast_vote(uid, vote)
		self._check_all_votes()
	
	def request_give_up(self, uid):
		if self._game_state != GAMESTATE.GUESSING:
			return
		if uid != self._player_order[self._current_guessing_idx]:
			return
		
		player = self._players[uid]
		player.success_round = -1
		self.broadcast_success(uid, -1, player.question)
		self._advance_to_next_player()

	def _start_countdown(self):
		if self._countdown_timer:
			return
		
		self._countdown_timer = threading.Timer(CONST.START_COUNTDOWN_DURATION, self._start_game)
		self._countdown_timer.start()
		self.broadcast_start_countdown()
	
	def _stop_countdown(self):
		if not self._countdown_timer:
			return
		
		self._countdown_timer.cancel()
		self._countdown_timer = None
		self.broadcast_start_countdown(is_stop=True)

	def _start_game(self):
		"""開始遊戲，設定玩家順序並要求出題。"""
		with self._manager.thread_lock:
			self._countdown_timer = None
			
			self._reset_game()
			self._current_round = 1
			self._game_state = GAMESTATE.PREPARING
			
			self._player_order = list(self._players.keys())
			random.shuffle(self._player_order)
			self._current_guessing_idx = 0
			
			self.broadcast_player_order(include_list=True)
			self.broadcast_start()

	def _reset_game(self):
		self._game_state = GAMESTATE.WAITING
		self._current_round = 0
		self._player_order = []
		self._current_guessing_idx = 0
		self._votes = {}
		
		self.temp_guess = ""
		
		for player in self._players.values():
			player.reset()

	def _check_all_given_words(self):
		"""檢查是否所有玩家都已出題。"""
		if self._game_state != GAMESTATE.PREPARING:
			return
		if any(player.question_locked == False for player in self._players.values()):
			return
		
		self._game_state = GAMESTATE.GUESSING
		self.broadcast_game_state()

	def _check_all_votes(self):
		"""檢查是否所有玩家都已投票。"""
		if self._game_state != GAMESTATE.VOTING:
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
			self.broadcast_guess_again()
		else:
			guessing_player_uid = self._player_order[self._current_guessing_idx]
			result = 1 if yes_votes > no_votes else 0
			self._players[guessing_player_uid].guess_history.append((self.temp_guess, result))
			self.broadcast_guess_record(guessing_player_uid, self.temp_guess, result)

		self._advance_to_next_player()

	def _advance_to_next_player(self):
		"""移動到下一個需要猜測的玩家。"""
		self.temp_guess = ""
		
		for i in range(len(self._player_order)):
			self._current_guessing_idx += 1
			if self._current_guessing_idx >= len(self._player_order):
				self._current_round += 1
				self._current_guessing_idx = 0
			
			next_uid = self._player_order[self._current_guessing_idx]
			# 跳過已經猜出的玩家
			if self._players[next_uid].success_round != 0:
				continue
			
			self._game_state = GAMESTATE.GUESSING
			
			self.broadcast_player_order()
			self.broadcast_game_state()
			return
		
		# 所有人都猜出來了
		self._reset_game()
		self.broadcast_end()
	
	# server messages ===========================================================================

	def send_init_packet(self, user):
		data = bytes()
		data += user.uid.to_bytes(2, byteorder="little")
		# 使用者列表
		data += len(self._user_ids).to_bytes(1, byteorder="little")
		for user_id in self._user_ids:
			target_user = self._manager.get_user(user_id)
			data += user_id.to_bytes(2, byteorder="little")
			encoded_name = target_user.name.encode("utf8")
			data += len(encoded_name).to_bytes(1, byteorder="little")
			data += encoded_name
		# 玩家列表
		data += len(self._players).to_bytes(1, byteorder="little")
		for player in self._players.values():
			data += player.user.uid.to_bytes(2, byteorder="little")
			encoded_question = player.question.encode("utf8")
			data += len(encoded_question).to_bytes(1, byteorder="little")
			data += encoded_question
			data += len(player.guess_history).to_bytes(1, byteorder="little")
			for guess in player.guess_history:
				encoded_guess = guess[0].encode("utf8")
				data += len(encoded_guess).to_bytes(1, byteorder="little")
				data += encoded_guess
				data += guess[1].to_bytes(1, byteorder="little")
			data += player.success_round.to_bytes(2, signed=True, byteorder="little")
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
		
		packet = new_packet(PROTOCOL_SERVER.INIT, data)
		try:
			user.socket.sendall(packet)
		except:
			pass

	def _broadcast(self, packet, exclude_client=None):
		"""廣播訊息給所有已連線的客戶端。"""
		disconnected_users = []
		for uid in self._user_ids:
			if uid != exclude_client:
				user = self._manager.get_user(uid)
				try:
					user.socket.sendall(packet)
				except:
					disconnected_users.append(user)
		
		for user in disconnected_users:
			self._manager.remove_user(user)
	
	def broadcast_connect(self, uid, name):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		encoded_name = name.encode("utf8")
		data += len(encoded_name).to_bytes(1, byteorder="little")
		data += encoded_name
		
		packet = new_packet(PROTOCOL_SERVER.CONNECT, data)
		self._broadcast(packet, exclude_client=uid)
	
	def broadcast_disconnect(self, uid):
		packet = new_packet(PROTOCOL_SERVER.DISCONNECT, uid.to_bytes(2, byteorder="little"))
		self._broadcast(packet)
	
	def broadcast_rename(self, uid, name):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		encoded_name = name.encode("utf8")
		data += len(encoded_name).to_bytes(1, byteorder="little")
		data += encoded_name
		
		packet = new_packet(PROTOCOL_SERVER.NAME, data)
		self._broadcast(packet)
	
	def broadcast_join(self, uid):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		
		packet = new_packet(PROTOCOL_SERVER.JOIN_GAME, data)
		self._broadcast(packet)
	
	def broadcast_leave(self, uid):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		
		packet = new_packet(PROTOCOL_SERVER.LEAVE_GAME, data)
		self._broadcast(packet)
	
	def broadcast_start_countdown(self, is_stop = False):
		data = bytes()
		if is_stop:
			data += int(0).to_bytes(1, byteorder="little")
		else:
			data += int(1).to_bytes(1, byteorder="little")
			data += CONST.START_COUNTDOWN_DURATION.to_bytes(1, byteorder="little")
		
		packet = new_packet(PROTOCOL_SERVER.START_COUNTDOWN, data)
		self._broadcast(packet)
	
	def broadcast_start(self):
		packet = new_packet(PROTOCOL_SERVER.START, bytes())
		self._broadcast(packet)
	
	def broadcast_game_state(self):
		packet = new_packet(PROTOCOL_SERVER.GAMESTATE, self._game_state.to_bytes(1, byteorder="little"))
		self._broadcast(packet)
	
	def broadcast_player_order(self, include_list = False):
		data = bytes()
		data += self._current_guessing_idx.to_bytes(1, byteorder="little")
		if include_list:
			data += int(1).to_bytes(1, byteorder="little")
			data += len(self._player_order).to_bytes(1, byteorder="little")
			for uid in self._player_order:
				data += uid.to_bytes(2, byteorder="little")
		else:
			data += int(0).to_bytes(1, byteorder="little")
		
		packet = new_packet(PROTOCOL_SERVER.PLAYER_ORDER, data)
		self._broadcast(packet)
	
	def broadcast_question(self, player):
		data = bytes()
		data += player.user.uid.to_bytes(2, byteorder="little")
		data += (1 if player.question_locked else 0).to_bytes(1, byteorder="little")
		encoded_question = player.question.encode("utf8")
		data += len(encoded_question).to_bytes(1, byteorder="little")
		data += encoded_question
		
		packet = new_packet(PROTOCOL_SERVER.QUESTION, data)
		self._broadcast(packet, exclude_client=player.user.uid)
		
		# 傳給玩家本身的資訊不含題目，只做提示已經出好題了
		data = bytes()
		data += player.user.uid.to_bytes(2, byteorder="little")
		data += (1 if player.question_locked else 0).to_bytes(1, byteorder="little")
		packet = new_packet(PROTOCOL_SERVER.QUESTION, data)
		try:
			player.user.socket.sendall(packet)
		except:
			pass
	
	def broadcast_success(self, uid, success_round, answer):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		data += success_round.to_bytes(2, signed=True, byteorder="little")
		
		encoded_answer = answer.encode("utf8")
		data += len(encoded_answer).to_bytes(1, byteorder="little")
		data += encoded_answer
		
		packet = new_packet(PROTOCOL_SERVER.SUCCESS, data)
		self._broadcast(packet)
	
	def broadcast_guess(self):
		data = bytes()
		encoded_guess = self.temp_guess.encode("utf8")
		data += len(encoded_guess).to_bytes(1, byteorder="little")
		data += encoded_guess
		
		packet = new_packet(PROTOCOL_SERVER.GUESS, data)
		self._broadcast(packet)
	
	def broadcast_vote(self, uid, vote):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		data += vote.to_bytes(1, byteorder="little")
		
		packet = new_packet(PROTOCOL_SERVER.VOTE, data)
		self._broadcast(packet)
	
	def broadcast_guess_again(self):
		packet = new_packet(PROTOCOL_SERVER.GUESS_AGAIN, bytes())
		self._broadcast(packet)
	
	def broadcast_guess_record(self, uid, guess, result):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		
		encoded_guess = guess.encode("utf8")
		data += len(encoded_guess).to_bytes(1, byteorder="little")
		data += encoded_guess
		
		data += result.to_bytes(1, byteorder="little")
		
		packet = new_packet(PROTOCOL_SERVER.GUESS_RECORD, data)
		self._broadcast(packet)
	
	def broadcast_end(self, is_force = False):
		end_type = 1 if is_force else 0
		packet = new_packet(PROTOCOL_SERVER.END, end_type.to_bytes(1, byteorder="little"))
		self._broadcast(packet)
	
	def broadcast_chat(self, uid, encoded_message, is_hidden):
		data = bytes()
		data += uid.to_bytes(2, byteorder="little")
		
		data += len(encoded_message).to_bytes(1, byteorder="little")
		data += encoded_message
		
		exclude_client = None
		if is_hidden:
			if self._game_state == GAMESTATE.GUESSING or self._game_state == GAMESTATE.VOTING:
				exclude_client = self._player_order[self._current_guessing_idx]
		if exclude_client == None:
			data += int(0).to_bytes(1, byteorder="little")
		else:
			data += int(1).to_bytes(1, byteorder="little")
		
		packet = new_packet(PROTOCOL_SERVER.CHAT, data)
		self._broadcast(packet, exclude_client)
	
	def broadcast_skip_guess(self, uid):
		packet = new_packet(PROTOCOL_SERVER.SKIP_GUESS, uid.to_bytes(2, byteorder="little"))
		self._broadcast(packet)


class GameManager:
	def __init__(self):
		self.thread_lock = threading.Lock()
		
		self.users = {}
		self.rooms = {}
	
	def _send_version_check_result(self, user):
		packet = new_packet(PROTOCOL_SERVER.VERSION, CONST.GAME_VERSION.to_bytes(4, byteorder="little"))
		try:
			user.socket.sendall(packet)
		except:
			pass
	
	def _send_room_id(self, user, id):
		# 正數時為進入的房間 ID，創建失敗為 -1，加入不存在房間為 -2
		packet = new_packet(PROTOCOL_SERVER.ROOM_ID, id.to_bytes(4, signed=True, byteorder="little"))
		try:
			user.socket.sendall(packet)
		except:
			pass
	
	def handle_client(self, conn, addr):
		"""處理單一客戶端的連線。"""
		with self.thread_lock:
			user = User.create(conn)
			if user == None:
				conn.close()
				print(f"同時連線數超過上限，中斷來自 {addr} 的連線")
				return
			
			self.users[user.uid] = user
			print(f"新連線：{user.uid} ({addr})")
		
		try:
			while True:
				header = conn.recv(5, socket.MSG_WAITALL)
				if not header:
					break
					
				protocol = header[0]
				packet_size = int.from_bytes(header[1:5], byteorder="little")
				
				data = bytes()
				is_disconnected = False
				while packet_size > 0:
					single_size = min(1024, packet_size)
					packet = conn.recv(single_size, socket.MSG_WAITALL)
					if not packet:
						is_disconnected = True
						break
					
					data += packet
					packet_size -= single_size
					
				if is_disconnected:
					break
				
				with self.thread_lock:
					self._process_message(user, protocol, data)
		except Exception as e:
			print(traceback.format_exc())
			print(f"{user.uid} ({addr}) 連線中斷")
		finally:
			with self.thread_lock:
				self.remove_user(user)
				conn.close()

	def _process_message(self, user, protocol, message):
		"""處理來自客戶端的訊息。"""
		if protocol == PROTOCOL_CLIENT.VERSION:
			if user.version_checked:
				return
			
			version = int.from_bytes(message, byteorder="little")
			self._send_version_check_result(user)
			if version != CONST.GAME_VERSION:
				user.socket.close()
				return
			
			user.version_checked = True
		elif protocol == PROTOCOL_CLIENT.NAME:
			if not user.check_version():
				return
			if len(message) > 255:
				return
			
			new_name = message.decode("utf8").strip()
			if new_name == user.name:
				return
			
			user.name = new_name
			print(f"使用者 {user.uid} 設定名稱為 {new_name}")
			
			if user.room_id >= 0:
				room = self.rooms.get(room_id)
				if room:
					room.broadcast_rename(user.uid, new_name)
		elif protocol == PROTOCOL_CLIENT.CREATE_ROOM:
			if not user.check_version():
				return
			if user.room_id >= 0:
				return
			
			room = GameRoom.create(self)
			if room == None:
				self._send_room_id(user, -1)
				return
			
			room_id = room.get_id()
			user.room_id = room_id
			room.add_user(user)
			self.rooms[room_id] = room
			
			self._send_room_id(user, room_id)
		elif protocol == PROTOCOL_CLIENT.JOIN_ROOM:
			if not user.check_version():
				return
			if user.room_id >= 0:
				return
			
			room_id = int.from_bytes(message, byteorder="little")
			room = self.rooms.get(room_id)
			if room == None:
				self._send_room_id(user, -2)
				return
			
			user.room_id = room_id
			room.add_user(user)
			
			self._send_room_id(user, room_id)
		elif protocol == PROTOCOL_CLIENT.LEAVE_ROOM:
			if user.room_id < 0:
				return
			
			self.user_leave_room(user)
			user.room_id = -1
		elif protocol == PROTOCOL_CLIENT.JOIN_GAME:
			if user.room_id < 0:
				return
			
			room = self.rooms[user.room_id]
			room.add_player(user)
		elif protocol == PROTOCOL_CLIENT.LEAVE_GAME:
			if user.room_id < 0:
				return
			
			room = self.rooms[user.room_id]
			room.remove_player(user.uid)
		elif protocol == PROTOCOL_CLIENT.START:
			if user.room_id < 0:
				return
			
			room = self.rooms[user.room_id]
			room.request_start(user.uid)
		elif protocol == PROTOCOL_CLIENT.CANCEL_START:
			if user.room_id < 0:
				return
			
			room = self.rooms[user.room_id]
			room.request_cancel_start(user.uid)
		elif protocol == PROTOCOL_CLIENT.QUESTION:
			if user.room_id < 0:
				return
			if len(message) > 256:
				return
			
			room = self.rooms[user.room_id]
			
			is_locked = message[0] == 1
			word = message[1:].decode("utf8").strip()
			room.request_assign_question(user.uid, word, is_locked)
		elif protocol == PROTOCOL_CLIENT.GUESS:
			if user.room_id < 0:
				return
			if len(message) > 255:
				return
			
			room = self.rooms[user.room_id]
			
			guess = message.decode("utf8").strip()
			room.request_guess(user.uid, guess)
		elif protocol == PROTOCOL_CLIENT.VOTE:
			if user.room_id < 0:
				return
			
			room = self.rooms[user.room_id]
			
			vote = int.from_bytes(message, byteorder="little")
			room.request_vote(user.uid, vote)
		elif protocol == PROTOCOL_CLIENT.CHAT:
			if user.room_id < 0:
				return
			if len(message) > 256:
				return
			
			room = self.rooms[user.room_id]
			
			is_hidden = message[0] == 1
			room.broadcast_chat(user.uid, message[1:], is_hidden)
		elif protocol == PROTOCOL_CLIENT.GIVE_UP:
			if user.room_id < 0:
				return
			
			room = self.rooms[user.room_id]
			room.request_give_up(user.uid)
	
	def get_user(self, uid):
		return self.users.get(uid)
	
	def user_leave_room(self, user):
		room = self.rooms.get(user.room_id)
		if room:
			room.remove_user(user.uid)
			print(f"玩家 {user.uid} 已離開房間 {user.room_id}")
			if room.is_empty():
				del self.rooms[user.room_id]
				print(f"已移除空房間 {user.room_id}")

	def remove_user(self, user):
		"""移除斷線的使用者"""
		if user.room_id >= 0:
			self.user_leave_room(user)
		del self.users[user.uid]
		print(f"玩家 {user.name} ({user.uid}) 已移除")

def main():
	HOST = '127.0.0.1'
	PORT = 11451
	
	"""伺服器主程式。"""
	server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
	try:
		server.bind((HOST, PORT))
	except socket.error as e:
		print(f"綁定 port 失敗：{e}")
		return
	server.listen()
	print(f"伺服器在 {HOST}:{PORT} 上監聽...")
	
	manager = GameManager()

	while True:
		conn, addr = server.accept()
		thread = threading.Thread(target=manager.handle_client, args=(conn, addr))
		thread.daemon = True
		thread.start()

if __name__ == "__main__":
	print("啟動猜題遊戲伺服器...")
	main()