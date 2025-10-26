using System;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20250103000000_AddDeliveryLogTimestamps")]
public partial class AddDeliveryLogTimestamps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAtUtc",
            table: "DeliveryLogs",
            type: "TEXT",
            nullable: false,
            defaultValueSql: "CURRENT_TIMESTAMP");

        migrationBuilder.AddColumn<DateTime>(
            name: "LastAttemptAtUtc",
            table: "DeliveryLogs",
            type: "TEXT",
            nullable: false,
            defaultValueSql: "CURRENT_TIMESTAMP");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CreatedAtUtc",
            table: "DeliveryLogs");

        migrationBuilder.DropColumn(
            name: "LastAttemptAtUtc",
            table: "DeliveryLogs");
    }
}
