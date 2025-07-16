import heapq
import random


class _SerialIDGenerator:
	def __init__(self, max_id):
		self._id_serial = 0
		self._free_id_list = []
		
		self._max_id = max_id

	def generate(self):
		if len(self._free_id_list) > 0:
			return heapq.heappop(self._free_id_list)
		
		self._id_serial += 1
		if self._id_serial > self._max_id:
			return -1
		return self._id_serial

	def release(self, id):
		heapq.heappush(self._free_id_list, id)

_user_id_generator = _SerialIDGenerator(0xffff)

def generate_user_id():
	return _user_id_generator.generate()

def release_user_id(id):
	_user_id_generator.release(id)


class _RandomIDGenerator:
	def __init__(self, max_id, *, max_generate_count=-1):
		if max_generate_count > max_id:
			raise Exception("Generation quota is greater than the total count of possible numbers")
		
		self._quota = max_generate_count
		self._available_id_list = list(range(1, max_id + 1))
	
	def generate(self):
		if self._quota == 0:
			return -1
		
		if self._quota > 0:
			self._quota -= 1
		
		max_index = len(self._available_id_list) - 1
		index = random.randint(0, max_index)
		self._available_id_list[index], self._available_id_list[max_index] = (
			self._available_id_list[max_index], self._available_id_list[index]
		)
		return self._available_id_list.pop()
	
	def release(self, id):
		self._available_id_list.append(id)

# 最多允許建立 100 個房間
_room_id_generator = _RandomIDGenerator(99999, max_generate_count=100)

def generate_room_id():
	return _room_id_generator.generate()

def release_room_id(id):
	_room_id_generator.release(id)
