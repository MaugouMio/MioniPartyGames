from game_define import PROTOCOL_SERVER


def new_packet(protocol: PROTOCOL_SERVER, data: bytes) -> bytes:
	packet = bytes()
	packet += protocol.to_bytes(1, byteorder="little")
	packet += data
	return packet