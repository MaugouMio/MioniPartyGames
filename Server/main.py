import os
import websockets
import ssl
import asyncio
import json

from managers.game_manager import GameManager


# load config
if os.path.isfile("config.json"):
	with open("config.json", "r") as f:
		CONFIG = json.loads(f.read())
else:
	CONFIG = dict()

async def main():
	HOST = CONFIG.get("HOST", "127.0.0.1")
	PORT = CONFIG.get("PORT", 11451)
	CERT_CHAIN = CONFIG.get("CERT_CHAIN")
	CERT_PRIVKEY = CONFIG.get("CERT_PRIVKEY")

	if CERT_CHAIN:
		ssl_context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
		ssl_context.load_cert_chain(CERT_CHAIN, CERT_PRIVKEY)
	else:
		ssl_context = None
	
	"""伺服器主程式。"""
	print(f"伺服器在 {HOST}:{PORT} 上監聽...")
	
	manager = GameManager()
	if int(websockets.__version__.split(".")[0]) >= 13:
		server = websockets.serve(manager.handle_client_new, HOST, PORT, ssl=ssl_context)
	else:
		server = websockets.serve(manager.handle_client, HOST, PORT, ssl=ssl_context)

	async with server:
		await asyncio.Future()  # run forever

if __name__ == "__main__":
	print("啟動喵喵小遊戲伺服器...")
	asyncio.run(main())