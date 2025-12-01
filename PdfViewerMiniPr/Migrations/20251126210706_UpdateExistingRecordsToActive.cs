using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PdfViewerMiniPr.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExistingRecordsToActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing records to be active
            migrationBuilder.Sql("UPDATE [Workflows] SET [IsActive] = 1 WHERE [IsActive] = 0");
            migrationBuilder.Sql("UPDATE [WorkflowStamps] SET [IsActive] = 1 WHERE [IsActive] = 0");
            migrationBuilder.Sql("UPDATE [WorkflowExternalAccesses] SET [IsActive] = 1 WHERE [IsActive] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
