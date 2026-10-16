-- NOTICE: This schema is still changing and will be locked at the first release.

CREATE TABLE metadata (
    "guid"     TEXT NOT NULL,
    "locale"   TEXT NOT NULL,

    "name"    TEXT,
    "overview" TEXT,

    "tagline" TEXT,

    "refreshed_at" TEXT NOT NULL,

    PRIMARY KEY ("guid", "locale")
);
