using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Hub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutputStreamStreamType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StreamType",
                table: "OutputStreams",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "stdout");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StreamType",
                table: "OutputStreams");
        }
    }
}
