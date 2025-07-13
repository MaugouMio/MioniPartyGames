import heapq


class _SerialIDGenerator:
	def __init__(self, max_id):
		self._uid_serial = 0
		self._free_uid = []
		
		self._max_id = max_id

	def generate(self):
		if len(self._free_uid) > 0:
			return heapq.heappop(self._free_uid)
		
		self._uid_serial += 1
		if self._uid_serial > self._max_id:
			return -1
		return self._uid_serial

	def release(self, uid):
		heapq.heappush(self._free_uid, uid)

_player_uid_generator = _SerialIDGenerator(0xffff)

def generate_player_uid():
	return _player_uid_generator.generate()

def release_player_uid(uid):
	_player_uid_generator.release(uid)
