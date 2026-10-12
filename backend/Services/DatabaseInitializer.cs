using System.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext database)
    {
        await database.Database.EnsureCreatedAsync();
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AvailableDates" (
                "Date" TEXT NOT NULL PRIMARY KEY
            );
            CREATE TABLE IF NOT EXISTS "UnavailableDates" (
                "Date" TEXT NOT NULL PRIMARY KEY
            );
            CREATE TABLE IF NOT EXISTS "Reservations" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Reservations" PRIMARY KEY AUTOINCREMENT,
                "CustomerId" TEXT NOT NULL DEFAULT '',
                "ReservationDate" TEXT NOT NULL,
                "ReservationTime" TEXT NOT NULL DEFAULT '',
                "CustomerName" TEXT NOT NULL,
                "Email" TEXT NOT NULL,
                "CustomerPhone" TEXT NOT NULL DEFAULT '',
                "City" TEXT NOT NULL DEFAULT '',
                "LocationAddress" TEXT NOT NULL DEFAULT '',
                "ServiceType" TEXT NOT NULL DEFAULT 'Workshop',
                "TravelFeeCents" INTEGER NOT NULL DEFAULT 0,
                "Status" TEXT NOT NULL DEFAULT 'Approved',
                "RejectionReason" TEXT NOT NULL DEFAULT '',
                "CreatedAtUtc" TEXT NOT NULL
            );
            """);

        await AddColumnIfMissingAsync(database, "Reservations", "ReservationTime", "TEXT NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync(database, "Reservations", "CustomerId", "TEXT NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync(database, "Reservations", "City", "TEXT NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync(database, "Reservations", "CustomerPhone", "TEXT NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync(database, "Reservations", "LocationAddress", "TEXT NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync(database, "Reservations", "ServiceType", "TEXT NOT NULL DEFAULT 'Workshop'");
        await AddColumnIfMissingAsync(database, "Reservations", "TravelFeeCents", "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync(database, "Reservations", "Status", "TEXT NOT NULL DEFAULT 'Approved'");
        await AddColumnIfMissingAsync(database, "Reservations", "RejectionReason", "TEXT NOT NULL DEFAULT ''");

        await database.Database.ExecuteSqlRawAsync("""
            WITH ranked AS (
                SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "ReservationDate" ORDER BY "Id") AS slot
                FROM "Reservations"
                WHERE "ReservationTime" = ''
            )
            UPDATE "Reservations"
            SET "ReservationTime" = printf('%02d:00', 8 + (
                SELECT slot FROM ranked WHERE ranked."Id" = "Reservations"."Id"
            ))
            WHERE "ReservationTime" = '';

            DROP INDEX IF EXISTS "IX_Reservations_ReservationDate_ReservationTime";
            CREATE INDEX IF NOT EXISTS "IX_Reservations_ReservationDate"
                ON "Reservations" ("ReservationDate");
            CREATE INDEX IF NOT EXISTS "IX_Reservations_ReservationDate_Status"
                ON "Reservations" ("ReservationDate", "Status");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Reservations_ReservationDate_ReservationTime"
                ON "Reservations" ("ReservationDate", "ReservationTime")
                WHERE "Status" != 'Rejected';
            """);

        await EnsureIdentitySchemaAsync(database);
    }

    private static async Task AddColumnIfMissingAsync(
        AppDbContext database,
        string table,
        string column,
        string definition)
    {
        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
        {
            await connection.OpenAsync();
        }

        var found = false;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.GetString(1) == column)
                {
                    found = true;
                    break;
                }
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        if (!found)
        {
            var alterConnection = database.Database.GetDbConnection();
            var closeAlterConnection = alterConnection.State != ConnectionState.Open;
            if (closeAlterConnection)
            {
                await alterConnection.OpenAsync();
            }

            try
            {
                await using var command = alterConnection.CreateCommand();
                command.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (closeAlterConnection)
                {
                    await alterConnection.CloseAsync();
                }
            }
        }
    }

    private static Task EnsureIdentitySchemaAsync(AppDbContext database) =>
        database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AspNetRoles" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
                "Name" TEXT NULL,
                "NormalizedName" TEXT NULL,
                "ConcurrencyStamp" TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS "AspNetUsers" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
                "UserName" TEXT NULL,
                "NormalizedUserName" TEXT NULL,
                "Email" TEXT NULL,
                "NormalizedEmail" TEXT NULL,
                "EmailConfirmed" INTEGER NOT NULL,
                "PasswordHash" TEXT NULL,
                "SecurityStamp" TEXT NULL,
                "ConcurrencyStamp" TEXT NULL,
                "PhoneNumber" TEXT NULL,
                "PhoneNumberConfirmed" INTEGER NOT NULL,
                "TwoFactorEnabled" INTEGER NOT NULL,
                "LockoutEnd" TEXT NULL,
                "LockoutEnabled" INTEGER NOT NULL,
                "AccessFailedCount" INTEGER NOT NULL,
                "DisplayName" TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS "AspNetRoleClaims" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
                "RoleId" TEXT NOT NULL,
                "ClaimType" TEXT NULL,
                "ClaimValue" TEXT NULL,
                CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId"
                    FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "AspNetUserClaims" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "ClaimType" TEXT NULL,
                "ClaimValue" TEXT NULL,
                CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "AspNetUserLogins" (
                "LoginProvider" TEXT NOT NULL,
                "ProviderKey" TEXT NOT NULL,
                "ProviderDisplayName" TEXT NULL,
                "UserId" TEXT NOT NULL,
                CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
                CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "AspNetUserRoles" (
                "UserId" TEXT NOT NULL,
                "RoleId" TEXT NOT NULL,
                CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
                CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId"
                    FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "AspNetUserTokens" (
                "UserId" TEXT NOT NULL,
                "LoginProvider" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Value" TEXT NULL,
                CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
                CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");
            CREATE UNIQUE INDEX IF NOT EXISTS "RoleNameIndex" ON "AspNetRoles" ("NormalizedName")
                WHERE "NormalizedName" IS NOT NULL;
            CREATE INDEX IF NOT EXISTS "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");
            CREATE INDEX IF NOT EXISTS "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");
            CREATE INDEX IF NOT EXISTS "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");
            CREATE INDEX IF NOT EXISTS "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");
            CREATE UNIQUE INDEX IF NOT EXISTS "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName")
                WHERE "NormalizedUserName" IS NOT NULL;
            """);
}
