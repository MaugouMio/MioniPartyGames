import enum


class CONST(enum.IntEnum):
	GAME_VERSION				= 3
	START_COUNTDOWN_DURATION	= 5

@enum.unique
class PROTOCOL_CLIENT(enum.IntEnum):
	NAME			= 0
	JOIN_GAME		= 1
	LEAVE_GAME		= 2
	START			= 3
	CANCEL_START	= 4
	QUESTION		= 5
	GUESS			= 6
	VOTE			= 7
	CHAT			= 8
	GIVE_UP			= 9
	VERSION			= 10
	CREATE_ROOM		= 11
	JOIN_ROOM		= 12
	LEAVE_ROOM		= 13

@enum.unique
class PROTOCOL_SERVER(enum.IntEnum):
	INIT			= 0
	CONNECT			= 1
	DISCONNECT		= 2
	NAME			= 3
	JOIN_GAME		= 4
	LEAVE_GAME		= 5
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
	CHAT			= 17
	SKIP_GUESS		= 18
	VERSION			= 19
	ROOM_ID			= 20

@enum.unique
class GAME_TYPE(enum.IntEnum):
	GUESS_WORD		= 0  # 猜名詞
	ARRANGE_NUMBER	= 1  # 數字排列

@enum.unique
class GUESS_WORD_STATE(enum.IntEnum):
	WAITING			= 0  # 可以加入遊戲的階段
	PREPARING		= 1  # 遊戲剛開始的出題階段
	GUESSING		= 2  # 某個玩家猜題當中
	VOTING			= 3  # 某個玩家猜測一個類別，等待其他人投票是否符合
