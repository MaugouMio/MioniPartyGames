import enum


class CONST(enum.IntEnum):
	GAME_VERSION				= 3
	START_COUNTDOWN_DURATION	= 5

@enum.unique
class PROTOCOL_CLIENT(enum.IntEnum):
	NAME					= enum.auto()
	JOIN_GAME				= enum.auto()
	LEAVE_GAME				= enum.auto()
	START					= enum.auto()
	CANCEL_START			= enum.auto()
	QUESTION				= enum.auto()
	GUESS					= enum.auto()
	VOTE					= enum.auto()
	CHAT					= enum.auto()
	GIVE_UP					= enum.auto()
	VERSION					= enum.auto()
	CREATE_ROOM				= enum.auto()
	JOIN_ROOM				= enum.auto()
	LEAVE_ROOM				= enum.auto()
	SET_MAX_NUMBER			= enum.auto()
	SET_NUMBER_GROUP_COUNT	= enum.auto()
	POSE_NUMBER				= enum.auto()

@enum.unique
class PROTOCOL_SERVER(enum.IntEnum):
	INIT			= enum.auto()
	CONNECT			= enum.auto()
	DISCONNECT		= enum.auto()
	NAME			= enum.auto()
	JOIN_GAME		= enum.auto()
	LEAVE_GAME		= enum.auto()
	START_COUNTDOWN	= enum.auto()
	START			= enum.auto()
	GAMESTATE		= enum.auto()
	PLAYER_ORDER	= enum.auto()
	QUESTION		= enum.auto()
	SUCCESS			= enum.auto()
	GUESS			= enum.auto()
	VOTE			= enum.auto()
	GUESS_AGAIN		= enum.auto()
	GUESS_RECORD	= enum.auto()
	END				= enum.auto()
	CHAT			= enum.auto()
	SKIP_GUESS		= enum.auto()
	VERSION			= enum.auto()
	ROOM_ID			= enum.auto()

@enum.unique
class GAME_TYPE(enum.Enum):
	GUESS_WORD		= enum.auto()  # 猜名詞
	ARRANGE_NUMBER	= enum.auto()  # 數字排列

@enum.unique
class GUESS_WORD_STATE(enum.Enum):
	WAITING			= enum.auto()  # 可以加入遊戲的階段
	PREPARING		= enum.auto()  # 遊戲剛開始的出題階段
	GUESSING		= enum.auto()  # 某個玩家猜題當中
	VOTING			= enum.auto()  # 某個玩家猜測一個類別，等待其他人投票是否符合

@enum.unique
class ARRANGE_NUMBER_STATE(enum.Enum):
	WAITING			= enum.auto()  # 可以加入與設定遊戲的階段
	PLAYING			= enum.auto()  # 遊戲進行中
