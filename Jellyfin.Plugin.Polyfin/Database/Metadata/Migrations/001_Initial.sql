CREATE TABLE metadata (
    "guid"     TEXT NOT NULL,
    "locale"   TEXT NOT NULL,

    "name"    TEXT,
    "overview" TEXT,

    PRIMARY KEY ("guid", "locale")
);
