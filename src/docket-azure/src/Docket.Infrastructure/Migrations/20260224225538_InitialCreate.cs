using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Docket.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Labels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeetingSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ExternalCalendarId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingSeries_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Minutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeriesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PreviousMinutesId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FinalizedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    AbandonedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AbandonedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    AbandonmentNote = table.Column<string>(type: "TEXT", nullable: true),
                    GlobalNote = table.Column<string>(type: "TEXT", nullable: true),
                    GlobalNotePinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Minutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Minutes_MeetingSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "MeetingSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Minutes_Minutes_PreviousMinutesId",
                        column: x => x.PreviousMinutesId,
                        principalTable: "Minutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Minutes_Users_AbandonedBy",
                        column: x => x.AbandonedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Minutes_Users_FinalizedBy",
                        column: x => x.FinalizedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SeriesParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeriesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesParticipants_MeetingSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "MeetingSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MinutesAttendees",
                columns: table => new
                {
                    MinutesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinutesAttendees", x => new { x.MinutesId, x.UserId });
                    table.ForeignKey(
                        name: "FK_MinutesAttendees_Minutes_MinutesId",
                        column: x => x.MinutesId,
                        principalTable: "Minutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MinutesAttendees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MinutesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceTopicId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsOpen = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSkipped = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponsibleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Topics_Minutes_MinutesId",
                        column: x => x.MinutesId,
                        principalTable: "Minutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Topics_Topics_SourceTopicId",
                        column: x => x.SourceTopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Topics_Users_ResponsibleId",
                        column: x => x.ResponsibleId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TopicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceActionItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ResponsibleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsRecurring = table.Column<bool>(type: "INTEGER", nullable: false),
                    AssignedInAbsentia = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionItems_ActionItems_SourceActionItemId",
                        column: x => x.SourceActionItemId,
                        principalTable: "ActionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActionItems_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionItems_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActionItems_Users_ResponsibleId",
                        column: x => x.ResponsibleId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InfoItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TopicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    PinnedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InfoItems_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InfoItems_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TopicLabels",
                columns: table => new
                {
                    TopicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LabelId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicLabels", x => new { x.TopicId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_TopicLabels_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TopicLabels_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionItemLabels",
                columns: table => new
                {
                    ActionItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LabelId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItemLabels", x => new { x.ActionItemId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_ActionItemLabels_ActionItems_ActionItemId",
                        column: x => x.ActionItemId,
                        principalTable: "ActionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionItemLabels_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionItemNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AuthorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItemNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionItemNotes_ActionItems_ActionItemId",
                        column: x => x.ActionItemId,
                        principalTable: "ActionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionItemNotes_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Labels",
                columns: new[] { "Id", "Category", "Color", "CreatedAt", "IsSystem", "Name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Action", "#1565C0", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Decision" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Action", "#6A1B9A", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Proposal" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Action", "#37474F", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "New" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Status", "#2E7D32", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Status:GREEN" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "Status", "#F57F17", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Status:YELLOW" },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "Status", "#B71C1C", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Status:RED" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemLabels_LabelId",
                table: "ActionItemLabels",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemNotes_ActionItemId_CreatedAt",
                table: "ActionItemNotes",
                columns: new[] { "ActionItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItemNotes_AuthorId",
                table: "ActionItemNotes",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_CreatedBy",
                table: "ActionItems",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_ResponsibleId",
                table: "ActionItems",
                column: "ResponsibleId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_SourceActionItemId",
                table: "ActionItems",
                column: "SourceActionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_Status",
                table: "ActionItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_TopicId",
                table: "ActionItems",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_InfoItems_CreatedBy",
                table: "InfoItems",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InfoItems_TopicId",
                table: "InfoItems",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Labels_Name",
                table: "Labels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingSeries_CreatedBy",
                table: "MeetingSeries",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Minutes_AbandonedBy",
                table: "Minutes",
                column: "AbandonedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Minutes_FinalizedBy",
                table: "Minutes",
                column: "FinalizedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Minutes_PreviousMinutesId",
                table: "Minutes",
                column: "PreviousMinutesId");

            migrationBuilder.CreateIndex(
                name: "IX_Minutes_ScheduledFor",
                table: "Minutes",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_Minutes_SeriesId",
                table: "Minutes",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_MinutesAttendees_UserId",
                table: "MinutesAttendees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesParticipants_SeriesId_UserId",
                table: "SeriesParticipants",
                columns: new[] { "SeriesId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeriesParticipants_UserId",
                table: "SeriesParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicLabels_LabelId",
                table: "TopicLabels",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_MinutesId",
                table: "Topics",
                column: "MinutesId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_ResponsibleId",
                table: "Topics",
                column: "ResponsibleId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_SourceTopicId",
                table: "Topics",
                column: "SourceTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalId",
                table: "Users",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionItemLabels");

            migrationBuilder.DropTable(
                name: "ActionItemNotes");

            migrationBuilder.DropTable(
                name: "InfoItems");

            migrationBuilder.DropTable(
                name: "MinutesAttendees");

            migrationBuilder.DropTable(
                name: "SeriesParticipants");

            migrationBuilder.DropTable(
                name: "TopicLabels");

            migrationBuilder.DropTable(
                name: "ActionItems");

            migrationBuilder.DropTable(
                name: "Labels");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "Minutes");

            migrationBuilder.DropTable(
                name: "MeetingSeries");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
