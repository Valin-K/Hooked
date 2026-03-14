using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hooked.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentEditedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FishingSessionId",
                table: "CatchRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "CatchComments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FishingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FishingSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FishingSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    LocationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Posts_FishingSessions_FishingSessionId",
                        column: x => x.FishingSessionId,
                        principalTable: "FishingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Posts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostPhotos_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatchRecords_FishingSessionId",
                table: "CatchRecords",
                column: "FishingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_FishingSessions_StartTime",
                table: "FishingSessions",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_FishingSessions_UserId",
                table: "FishingSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostPhotos_PostId",
                table: "PostPhotos",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CreatedAt",
                table: "Posts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_FishingSessionId",
                table: "Posts",
                column: "FishingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_UserId",
                table: "Posts",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CatchRecords_FishingSessions_FishingSessionId",
                table: "CatchRecords",
                column: "FishingSessionId",
                principalTable: "FishingSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatchRecords_FishingSessions_FishingSessionId",
                table: "CatchRecords");

            migrationBuilder.DropTable(
                name: "PostPhotos");

            migrationBuilder.DropTable(
                name: "Posts");

            migrationBuilder.DropTable(
                name: "FishingSessions");

            migrationBuilder.DropIndex(
                name: "IX_CatchRecords_FishingSessionId",
                table: "CatchRecords");

            migrationBuilder.DropColumn(
                name: "FishingSessionId",
                table: "CatchRecords");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "CatchComments");
        }
    }
}
