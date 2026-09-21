CREATE TABLE metadata (
    "guid"     TEXT NOT NULL,
    "language" TEXT NOT NULL,

    "name"    TEXT,
    "overview" TEXT,

    PRIMARY KEY ("guid", "language")
);
