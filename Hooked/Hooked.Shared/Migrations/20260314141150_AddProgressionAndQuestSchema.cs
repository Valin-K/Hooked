using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hooked.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressionAndQuestSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "CatchRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fishing_quests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Cadence = table.Column<int>(type: "integer", nullable: false),
                    TargetCount = table.Column<int>(type: "integer", nullable: false),
                    RewardXp = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fishing_quests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fishing_quests_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    CurrentLevel = table.Column<int>(type: "integer", nullable: false),
                    CurrentXp = table.Column<int>(type: "integer", nullable: false),
                    TotalXpEarned = table.Column<int>(type: "integer", nullable: false),
                    FirstAchievedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_skills_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_skills_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "xp_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    XpDelta = table.Column<int>(type: "integer", nullable: false),
                    PreviousLevel = table.Column<int>(type: "integer", nullable: false),
                    NewLevel = table.Column<int>(type: "integer", nullable: false),
                    PreviousXp = table.Column<int>(type: "integer", nullable: false),
                    NewXp = table.Column<int>(type: "integer", nullable: false),
                    LevelsGained = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CatchRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xp_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_xp_events_CatchRecords_CatchRecordId",
                        column: x => x.CatchRecordId,
                        principalTable: "CatchRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_xp_events_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_xp_events_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_fishing_quest_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProgressCount = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RewardClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RewardXpEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_fishing_quest_progress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_fishing_quest_progress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_fishing_quest_progress_fishing_quests_QuestId",
                        column: x => x.QuestId,
                        principalTable: "fishing_quests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_fishing_quest_progress_xp_events_RewardXpEventId",
                        column: x => x.RewardXpEventId,
                        principalTable: "xp_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatchRecords_UserId_IsFavorite",
                table: "CatchRecords",
                columns: new[] { "UserId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "IX_fishing_quests_Cadence_IsActive",
                table: "fishing_quests",
                columns: new[] { "Cadence", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_fishing_quests_Key",
                table: "fishing_quests",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fishing_quests_SkillId",
                table: "fishing_quests",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_skills_Key",
                table: "skills",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_fishing_quest_progress_QuestId",
                table: "user_fishing_quest_progress",
                column: "QuestId");

            migrationBuilder.CreateIndex(
                name: "IX_user_fishing_quest_progress_RewardXpEventId",
                table: "user_fishing_quest_progress",
                column: "RewardXpEventId");

            migrationBuilder.CreateIndex(
                name: "IX_user_fishing_quest_progress_UserId_IsCompleted_RewardClaime~",
                table: "user_fishing_quest_progress",
                columns: new[] { "UserId", "IsCompleted", "RewardClaimedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_fishing_quest_progress_UserId_PeriodStartUtc_PeriodEnd~",
                table: "user_fishing_quest_progress",
                columns: new[] { "UserId", "PeriodStartUtc", "PeriodEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_fishing_quest_progress_UserId_QuestId_PeriodStartUtc",
                table: "user_fishing_quest_progress",
                columns: new[] { "UserId", "QuestId", "PeriodStartUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_skills_LastUpdatedAt",
                table: "user_skills",
                column: "LastUpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_skills_SkillId",
                table: "user_skills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_user_skills_UserId_SkillId",
                table: "user_skills",
                columns: new[] { "UserId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_xp_events_CatchRecordId",
                table: "xp_events",
                column: "CatchRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_xp_events_EventKey",
                table: "xp_events",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_xp_events_SkillId",
                table: "xp_events",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_xp_events_UserId_SkillId_CreatedAt",
                table: "xp_events",
                columns: new[] { "UserId", "SkillId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_fishing_quest_progress");

            migrationBuilder.DropTable(
                name: "user_skills");

            migrationBuilder.DropTable(
                name: "fishing_quests");

            migrationBuilder.DropTable(
                name: "xp_events");

            migrationBuilder.DropTable(
                name: "skills");

            migrationBuilder.DropIndex(
                name: "IX_CatchRecords_UserId_IsFavorite",
                table: "CatchRecords");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "CatchRecords");
        }
    }
}
