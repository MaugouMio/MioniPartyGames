import socket
import threading
import random
import time

class GLOBAL:
	uid_serial = 1
	
	# 簡單流水號處理，預期不會有人從 1 號掛機到 65535 號用完還沒斷線
	@classmethod
	def GenerateUID(cls):
		new_uid = cls.uid_serial
		cls.uid_serial += 1
		if cls.uid_serial > 0xffff:
			cls.uid_serial = 1
		return new_uid

class PROTOCOL_CLIENT:
	NAME			= 0
	JOIN			= 1
	LEAVE			= 2
	START			= 3
	CANCEL_START	= 4
	QUESTION		= 5
	GUESS			= 6
	VOTE			= 7

class PROTOCOL_SERVER:
	INIT			= 0
	CONNECT			= 1
	DISCONNECT		= 2
	NAME			= 3
	JOIN			= 4
	LEAVE			= 5
	START_COUNTDOWN	= 6
	START			= 7
	GAMESTATE		= 8
	PLAYER_ORDER	= 9
	QUESTION		= 10
	SUCCESS			= 11
	GUESS			= 12
	VOTE			= 13
	GUESS_AGAIN		= 14
	GUESS_RECORD	= 15
	END				= 16

class GAMESTATE:
	WAITING			= 0  # 可以加入遊戲的階段
	PREPARING		= 1  # 遊戲剛開始的出題階段
	GUESSING		= 2  # 某個玩家猜題當中
	VOTING			= 3  # 某個玩家猜測一個類別，等待其他人投票是否符合

class User:
	def __init__(self, conn, id):
		self.socket = conn
		self.uid = id
		self.name = f"Player_{id}"

class Player:
	def __init__(self, user):
		self.user = user
		self.reset()
	
	def reset(self):
		self.question = ""
		self.guess_history = []
		self.success_round = 0

class GameManager:
	START_COUNTDOWN_DURATION = 5
	def __init__(self):
		self.thread_lock = threading.Lock()
		
		self.users = {}
		self.players = {}
		self.countdown_timer = None
		
		self.reset_game()

	def reset_game(self):
		self.game_state = GAMESTATE.WAITING
		self.current_round = 0
		self.player_order = []
		self.current_guessing_idx = 0
		self.temp_guess = ""
		self.votes = {}
		for player in self.players.values():
			player.reset()

	def new_packet(self, protocol, data):
		packet = bytes()
		packet += protocol.to_bytes(1, byteorder='little')
		packet += len(data).to_bytes(4, byteorder='little')
		packet += data
		return packet

	def broadcast(self, packet, exclude_client=None):
		"""廣播訊息給所有已連線的客戶端。"""
		for uid, user in self.users.items():
			if uid != exclude_client:
				try:
					user.socket.sendall(packet)
				except:
					self.remove_user(uid)

	def send_init_packet(self, uid):
		data = bytes()
		data += uid.to_bytes(2, byteorder='little')
		# 使用者列表
		data += len(self.users).to_bytes(1, byteorder='little')
		for user in self.users.values():
			data += user.uid.to_bytes(2, byteorder='little')
			encoded_name = user.name.encode('utf8')
			data += len(encoded_name).to_bytes(1, byteorder='little')
			data += encoded_name
		# 玩家列表
		data += len(self.players).to_bytes(1, byteorder='little')
		for player in self.players.values():
			data += player.user.uid.to_bytes(2, byteorder='little')
			encoded_question = player.question.encode('utf8')
			data += len(encoded_question).to_bytes(1, byteorder='little')
			data += encoded_question
			data += len(player.guess_history).to_bytes(1, byteorder='little')
			for guess in players.guess_history:
				encoded_guess = guess[0].encode('utf8')
				data += len(encoded_guess).to_bytes(1, byteorder='little')
				data += encoded_guess
				data += guess[1].to_bytes(1, byteorder='little')
			data += player.success_round.to_bytes(2, byteorder='little')
		# 遊戲階段
		data += self.game_state.to_bytes(1, byteorder='little')
		# 玩家順序
		data += len(self.player_order).to_bytes(1, byteorder='little')
		for player_uid in self.player_order:
			data += player_uid.to_bytes(2, byteorder='little')
		data += self.current_guessing_idx.to_bytes(1, byteorder='little')
		# 投票狀況
		encoded_guess = self.temp_guess.encode('utf8')
		data += len(encoded_guess).to_bytes(1, byteorder='little')
		data += encoded_guess
		
		data += len(self.votes).to_bytes(1, byteorder='little')
		for vote_uid, vote in self.votes.items():
			data += vote_uid.to_bytes(2, byteorder='little')
			data += vote.to_bytes(1, byteorder='little')
		
		packet = self.new_packet(PROTOCOL_SERVER.INIT, data)
		user = self.users[uid]
		try:
			user.socket.sendall(packet)
		except:
			pass
	
	def broadcast_connect(self, uid):
		packet = self.new_packet(PROTOCOL_SERVER.CONNECT, uid.to_bytes(2, byteorder='little'))
		self.broadcast(packet, exclude_client=uid)
	
	def broadcast_disconnect(self, uid):
		packet = self.new_packet(PROTOCOL_SERVER.DISCONNECT, uid.to_bytes(2, byteorder='little'))
		self.broadcast(packet)
	
	def broadcast_rename(self, uid, name):
		data = bytes()
		data += uid.to_bytes(2, byteorder='little')
		encoded_name = name.encode('utf8')
		data += len(encoded_name).to_bytes(1, byteorder='little')
		data += encoded_name
		
		packet = self.new_packet(PROTOCOL_SERVER.NAME, data)
		self.broadcast(packet)
	
	def broadcast_join(self, uid):
		data = bytes()
		data += uid.to_bytes(2, byteorder='little')
		
		packet = self.new_packet(PROTOCOL_SERVER.JOIN, data)
		self.broadcast(packet)
	
	def broadcast_leave(self, uid):
		data = bytes()
		data += uid.to_bytes(2, byteorder='little')
		
		packet = self.new_packet(PROTOCOL_SERVER.LEAVE, data)
		self.broadcast(packet)
	
	def broadcast_start_countdown(self, is_stop = False):
		data = bytes()
		if is_stop:
			data += int(0).to_bytes(1, byteorder='little')
		else:
			data += int(1).to_bytes(1, byteorder='little')
			data += GameManager.START_COUNTDOWN_DURATION.to_bytes(1, byteorder='little')
		
		packet = self.new_packet(PROTOCOL_SERVER.START_COUNTDOWN, data)
		self.broadcast(packet)
	
	def broadcast_start(self):
		packet = self.new_packet(PROTOCOL_SERVER.START, bytes())
		self.broadcast(packet)
	
	def broadcast_game_state(self):
		packet = self.new_packet(PROTOCOL_SERVER.GAMESTATE, self.game_state.to_bytes(1, byteorder='little'))
		self.broadcast(packet)
	
	def broadcast_player_order(self, include_list = False):
		data = bytes()
		data += self.current_guessing_idx.to_bytes(1, byteorder='little')
		if include_list:
			data += int(1).to_bytes(1, byteorder='little')
			data += len(self.player_order).to_bytes(1, byteorder='little')
			for uid in self.player_order:
				data += uid.to_bytes(2, byteorder='little')
		else:
			data += int(0).to_bytes(1, byteorder='little')
		
		packet = self.new_packet(PROTOCOL_SERVER.GAMESTATE, data)
		self.broadcast(packet)
	
	def broadcast_question(self, player):
		data = bytes()
		data += player.uid.to_bytes(2, byteorder='little')
		encoded_question = player.question.encode('utf8')
		data += len(encoded_question).to_bytes(1, byteorder='little')
		data += encoded_question
		
		packet = self.new_packet(PROTOCOL_SERVER.QUESTION, data)
		self.broadcast(packet, exclude_client=player.uid)
		
		# 傳給玩家本身的資訊不含題目，只做提示已經出好題了
		data = bytes()
		data += player.uid.to_bytes(2, byteorder='little')
		packet = self.new_packet(PROTOCOL_SERVER.QUESTION, data)
		try:
			player.user.socket.sendall(packet)
		except:
			pass
	
	def broadcast_success(self, uid, success_round):
		data = bytes()
		data += uid.to_bytes(2, byteorder='little')
		data += success_round.to_bytes(2, byteorder='little')
		
		packet = self.new_packet(PROTOCOL_SERVER.SUCCESS, data)
		self.broadcast(packet)
	
	def broadcast_guess(self):
		data = bytes()
		encoded_guess = self.temp_guess.encode('utf8')
		data += len(encoded_guess).to_bytes(2, byteorder='little')
		data += encoded_guess
		
		packet = self.new_packet(PROTOCOL_SERVER.GUESS, data)
		self.broadcast(packet)
	
	def broadcast_vote(self, uid, vote):
		data = bytes()
		data += uid.to_bytes(2, byteorder='little')
		data += vote.to_bytes(1, byteorder='little')
		
		packet = self.new_packet(PROTOCOL_SERVER.VOTE, data)
		self.broadcast(packet)
	
	def broadcast_guess_again(self):
		packet = self.new_packet(PROTOCOL_SERVER.GUESS_AGAIN, bytes())
		self.broadcast(packet)
	
	def broadcast_guess_record(self, uid, guess, result):
		data = bytes()
		data += uid.to_bytes(2, byteorder='little')
		
		encoded_guess = guess.encode('utf8')
		data += len(encoded_guess).to_bytes(1, byteorder='little')
		data += encoded_guess
		
		data += result.to_bytes(1, byteorder='little')
		
		packet = self.new_packet(PROTOCOL_SERVER.GUESS_RECORD, data)
		self.broadcast(packet)
	
	def broadcast_end(self, is_force = False):
		end_type = 1 if is_force else 0
		packet = self.new_packet(PROTOCOL_SERVER.END, end_type.to_bytes(1, byteorder='little'))
		self.broadcast(packet)
	
	def handle_client(self, conn, addr):
		"""處理單一客戶端的連線。"""
		self.thread_lock.acquire()
		
		uid = GLOBAL.GenerateUID()
		user = User(conn, uid)
		self.users[uid] = user
		print(f"新連線：{uid} ({addr})")
		
		# 傳送初始資料封包
		self.send_init_packet(uid)
		self.broadcast_connect(uid)
		
		self.thread_lock.release()

		try:
			while True:
				header = conn.recv(8, socket.MSG_WAITALL)
				if not header:
					break
					
				protocol = int.from_bytes(header[0], byteorder='little')
				packet_size = int.from_bytes(header[1:5], byteorder='little')
				print(f"收到來自 {uid} ({addr}) 的 protocol：{protocol} 長度：{packet_size}")
				
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
				
				self.thread_lock.acquire()
				self.process_message(user, protocol, data)
				self.thread_lock.release()
		except:
			print(f"{uid} ({addr}) 連線中斷")
		finally:
			self.thread_lock.acquire()
			
			self.remove_user(uid)
			conn.close()
			self.broadcast_disconnect(uid)
			
			self.thread_lock.release()

	def process_message(self, user, protocol, message):
		"""處理來自客戶端的訊息。"""
		if protocol == PROTOCOL_CLIENT.NAME:
			if len(message) > 255:
				return
			
			new_name = message.decode('utf8').strip()
			if new_name == user.name:
				return
			
			user.name = new_name
			self.broadcast_rename(user.uid, new_name)
		elif protocol == PROTOCOL_CLIENT.JOIN:
			if self.game_state != GAMESTATE.WAITING:
				return
			if user.uid in self.players:
				return
			
			player = Player(user)
			self.players[user.uid] = player
			
			self.stop_countdown()
			self.broadcast_join(user.uid)
		elif protocol == PROTOCOL_CLIENT.LEAVE:
			self.remove_player(user.uid)
		elif protocol == PROTOCOL_CLIENT.START:
			if self.game_state != GAMESTATE.WAITING:
				return
			if user.uid not in self.players:
				return
			if len(self.players) < 2:
				return
			
			self.start_countdown()
		elif protocol == PROTOCOL_CLIENT.CANCEL_START:
			if self.game_state != GAMESTATE.WAITING:
				return
			if user.uid not in self.players:
				return
			
			self.stop_countdown()
		elif protocol == PROTOCOL_CLIENT.QUESTION:
			if self.game_state != GAMESTATE.PREPARING:
				return
			if len(message) > 255:
				return
			if user.uid not in self.players:
				return
			
			next_player = self.players[self.player_order[(self.player_order.index(user.uid) + 1) % len(self.player_order)]]
			if next_player.question != "":
				return
			
			word = message.decode('utf8').strip()
			if word == "":
				return
			
			next_player.question = word
			self.broadcast_question(next_player)
			
			self.check_all_given_words()
		elif protocol == PROTOCOL_CLIENT.GUESS:
			if self.game_state != GAMESTATE.GUESSING:
				return
			if len(message) > 255:
				return
			if user.uid != self.player_order[self.current_guessing_idx]:
				return
			
			player = self.players[user.uid]
			guess = message.decode('utf8').strip()
			if guess == player.question:
				player.success_round = self.current_round
				self.broadcast_success(user.uid, self.current_round)
				self.advance_to_next_player()
				return
			
			self.temp_guess = guess
			self.votes.clear()
			self.game_state = GAMESTATE.VOTING
			
			self.broadcast_guess()
		elif protocol == PROTOCOL_CLIENT.VOTE:
			if self.game_state != GAMESTATE.VOTING:
				return
			if user.uid not in self.players:
				return
			if user.uid == self.player_order[self.current_guessing_idx]:
				return
			
			vote = int.from_bytes(message, byteorder='little')
			if vote < 0 or vote > 2:
				return
			
			self.votes[user.uid] = vote
			self.broadcast_vote(uid, vote)
			self.check_all_votes()

	def start_countdown(self):
		if self.countdown_timer:
			return
		
		self.countdown_timer = threading.Timer(GameManager.START_COUNTDOWN_DURATION, self.start_game)
		self.countdown_timer.start()
		self.broadcast_start_countdown()
	
	def stop_countdown(self):
		if not self.countdown_timer:
			return
		
		self.countdown_timer.cancel()
		self.countdown_timer = None
		self.broadcast_start_countdown(is_stop=True)

	def start_game(self):
		self.thread_lock.acquire()
		
		"""開始遊戲，設定玩家順序並要求出題。"""
		self.player_order = list(self.players.keys())
		random.shuffle(self.player_order)
		self.current_guessing_idx = 0
		
		for uid, player in self.players.items():
			player.reset()
		
		self.reset_game()
		self.current_round = 1
		self.game_state = GAMESTATE.PREPARING
		
		self.broadcast_player_order(include_list=True)
		self.broadcast_start()
		
		self.thread_lock.release()

	def check_all_given_words(self):
		"""檢查是否所有玩家都已出題。"""
		if self.game_state != GAMESTATE.PREPARING:
			return
		if any(player.question == "" for player in self.players.values()):
			return
		
		self.game_state = GAMESTATE.GUESSING
		self.broadcast_game_state()

	def check_all_votes(self):
		"""檢查是否所有玩家都已投票。"""
		if self.game_state != GAMESTATE.VOTING:
			return
		if len(self.votes) < len(self.players) - 1:
			return
		
		yes_votes = 0
		no_votes = 0
		abstain_votes = 0
		for vote in self.votes.values():
			if vote == 1:
				yes_votes += 1
			elif vote == 2:
				no_votes += 1
			else:
				abstain_votes += 1
		
		if yes_votes == 0 and no_votes == 0:
			self.current_guessing_idx -= 1  # 無效投票，讓玩家再猜一個類型
			self.broadcast_guess_again()
		else:
			guessing_player_uid = self.player_order[self.current_guessing_idx]
			result = 1 if yes_votes >= no_votes else 0
			self.players[guessing_player_uid].guess_history.append((self.temp_guess, result))
			self.broadcast_guess_record(guessing_player_uid, self.temp_guess, result)

		self.advance_to_next_player()

	def advance_to_next_player(self):
		"""移動到下一個需要猜測的玩家。"""
		self.temp_guess = ""
		
		for i in range(len(self.player_order)):
			self.current_guessing_idx += 1
			if self.current_guessing_idx >= len(self.player_order):
				self.current_round += 1
				self.current_guessing_idx = 0
			
			next_uid = self.player_order[self.current_guessing_idx]
			# 跳過已經猜出的玩家
			if self.players[next_uid].success_round > 0:
				continue
			
			self.game_state = GAMESTATE.GUESSING
			
			self.broadcast_player_order()
			self.broadcast_game_state()
			return
		
		# 所有人都猜出來了
		self.reset_game()
		self.broadcast_end()
	
	def remove_player(self, uid):
		if uid not in self.players:
			return
		
		self.stop_countdown()
		nickname = self.players[uid].name
		del self.players[uid]
		if self.game_state != GAMESTATE.WAITING:
			if len(self.players) < 2:
				self.reset_game()
				self.broadcast_leave(uid)
				self.broadcast_end(True)
				return
			
			order_index = self.player_order.index(uid)
			del self.player_order[order_index]
			if order_index == self.current_guessing_idx:
				self.current_guessing_idx -= 1  # 後面會遞補回這個 index 所以要往回退一格，下一個才會是現在這個 index
				self.advance_to_next_player()
		
		if uid in self.votes:
			del self.votes[uid]
		
		self.check_all_given_words()
		self.check_all_votes()
		self.broadcast_leave(uid)

	def remove_user(self, uid):
		"""移除斷線的使用者"""
		if uid in self.users:
			print(f"玩家 {self.users[uid].name} ({uid}) 已移除")
			del self.users[uid]
		
		self.remove_player(uid)

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