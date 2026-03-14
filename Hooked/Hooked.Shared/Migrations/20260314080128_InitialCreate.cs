using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hooked.Shared.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Achievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achievements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FishSpecies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ScientificName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConservationStatus = table.Column<string>(type: "text", nullable: true),
                    IsInvasive = table.Column<bool>(type: "boolean", nullable: false),
                    IsEndangered = table.Column<bool>(type: "boolean", nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DiscoveredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IllustrationImageUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IllustrationGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishSpecies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FishSpecies_Users_DiscoveredByUserId",
                        column: x => x.DiscoveredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FriendRelations",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FriendId = table.Column<Guid>(type: "uuid", nullable: false),
                    Since = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendRelations", x => new { x.UserId, x.FriendId });
                    table.ForeignKey(
                        name: "FK_FriendRelations_Users_FriendId",
                        column: x => x.FriendId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FriendRelations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaderboardEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaderboardEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatchRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeciesId = table.Column<int>(type: "integer", nullable: false),
                    CaughtAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LengthMeters = table.Column<double>(type: "double precision", nullable: true),
                    WeightKg = table.Column<double>(type: "double precision", nullable: true),
                    PhotoPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LocationJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatchRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatchRecords_FishSpecies_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "FishSpecies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatchRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sightings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeciesId = table.Column<int>(type: "integer", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    LocationJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sightings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sightings_FishSpecies_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "FishSpecies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sightings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatchComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CommentedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatchComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatchComments_CatchRecords_CatchId",
                        column: x => x.CatchId,
                        principalTable: "CatchRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatchComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatchReactions",
                columns: table => new
                {
                    CatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatchReactions", x => new { x.CatchId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CatchReactions_CatchRecords_CatchId",
                        column: x => x.CatchId,
                        principalTable: "CatchRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatchReactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FishDexEntries",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeciesId = table.Column<int>(type: "integer", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRare = table.Column<bool>(type: "boolean", nullable: false),
                    CatchCount = table.Column<int>(type: "integer", nullable: false),
                    PersonalBestLengthMeters = table.Column<double>(type: "double precision", nullable: true),
                    PersonalBestCatchId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishDexEntries", x => new { x.UserId, x.SpeciesId });
                    table.ForeignKey(
                        name: "FK_FishDexEntries_CatchRecords_PersonalBestCatchId",
                        column: x => x.PersonalBestCatchId,
                        principalTable: "CatchRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FishDexEntries_FishSpecies_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "FishSpecies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FishDexEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_Key",
                table: "Achievements",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatchComments_CatchId",
                table: "CatchComments",
                column: "CatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CatchComments_CommentedAt",
                table: "CatchComments",
                column: "CommentedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CatchComments_UserId",
                table: "CatchComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CatchReactions_ReactedAt",
                table: "CatchReactions",
                column: "ReactedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CatchReactions_UserId",
                table: "CatchReactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CatchRecords_CaughtAt",
                table: "CatchRecords",
                column: "CaughtAt");

            migrationBuilder.CreateIndex(
                name: "IX_CatchRecords_SpeciesId",
                table: "CatchRecords",
                column: "SpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_CatchRecords_UserId",
                table: "CatchRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FishDexEntries_PersonalBestCatchId",
                table: "FishDexEntries",
                column: "PersonalBestCatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FishDexEntries_SpeciesId",
                table: "FishDexEntries",
                column: "SpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_FishDexEntries_UnlockedAt",
                table: "FishDexEntries",
                column: "UnlockedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FishSpecies_CommonName",
                table: "FishSpecies",
                column: "CommonName");

            migrationBuilder.CreateIndex(
                name: "IX_FishSpecies_DiscoveredByUserId",
                table: "FishSpecies",
                column: "DiscoveredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRelations_FriendId",
                table: "FriendRelations",
                column: "FriendId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardEntries_Category_Score",
                table: "LeaderboardEntries",
                columns: new[] { "Category", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardEntries_UserId",
                table: "LeaderboardEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sightings_ReportedAt",
                table: "Sightings",
                column: "ReportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sightings_SpeciesId",
                table: "Sightings",
                column: "SpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_Sightings_UserId",
                table: "Sightings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Achievements");

            migrationBuilder.DropTable(
                name: "CatchComments");

            migrationBuilder.DropTable(
                name: "CatchReactions");

            migrationBuilder.DropTable(
                name: "FishDexEntries");

            migrationBuilder.DropTable(
                name: "FriendRelations");

            migrationBuilder.DropTable(
                name: "LeaderboardEntries");

            migrationBuilder.DropTable(
                name: "Sightings");

            migrationBuilder.DropTable(
                name: "CatchRecords");

            migrationBuilder.DropTable(
                name: "FishSpecies");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
