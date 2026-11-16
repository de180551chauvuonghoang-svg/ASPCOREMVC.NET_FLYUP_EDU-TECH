BEGIN;

CREATE TABLE IF NOT EXISTS "SePayReceipts" (
    "TransactionId" bigint PRIMARY KEY,
    "OrderId" integer NOT NULL,
    "CourseId" integer NOT NULL,
    "CourseTitle" varchar(200) NOT NULL,
    "InstructorId" varchar(450) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "VerifiedAtUtc" timestamptz NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SePayReceipts_OrderId" ON "SePayReceipts" ("OrderId");
CREATE INDEX IF NOT EXISTS "IX_SePayReceipts_InstructorId" ON "SePayReceipts" ("InstructorId");

COMMIT;
