using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using eSureHi.Data;
using eSureHi.Models;
using QuestDocument = QuestPDF.Fluent.Document;
using Microsoft.Win32;

namespace eSureHi.Services
{
    public static class ReportExportService
    {
        private const string SysName = "eSureHi - Insurance Management System";
        private const string MunicipalityName = "MUNICIPALITY OF SULOP";
        private const string SulopSealFile = "sulop_seal.png";
        private const string AppLogoFile = "logo.png";

        private static CompanyProfile GetCompanyProfile()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                return db.CompanyProfiles.FirstOrDefault() ?? new CompanyProfile { Name = "Municipality of Sulop", Address = "" };
            }
            catch
            {
                return new CompanyProfile { Name = "Municipality of Sulop", Address = "" };
            }
        }

        private static string? GetAssetPath(string fileName)
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName),
                Path.Combine(Environment.CurrentDirectory, "Assets", fileName),
                Path.Combine(Environment.CurrentDirectory, "OCIMS", "Assets", fileName)
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        // ══════════════════════════════════════════════════════════════
        // EXCEL
        // ══════════════════════════════════════════════════════════════

        public static void ExportEmployeeCoverageExcel(
            IEnumerable<VwEmployeeCoverage> data, string title)
        {
            var profile = GetCompanyProfile();
            var path = PickSavePath("Employee_Coverage", "xlsx");
            if (path is null) return;
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Employee Coverage");
            WriteExcelTitle(ws, title, profile);
            string[] h = {
                "Household/Resident ID","Full Name","Department","Policy Name",
                "Policy Type","Coverage Limit","Emp Share","Employer Share",
                "Start Date","End Date","Status"
            };
            WriteHeaders(ws, 5, h);
            int row = 6;
            foreach (var r in data)
            {
                ws.Cell(row, 1).Value = r.EmployeeNo;
                ws.Cell(row, 2).Value = r.FullName;
                ws.Cell(row, 3).Value = r.DeptName;
                ws.Cell(row, 4).Value = r.PolicyName;
                ws.Cell(row, 5).Value = r.PolicyType;
                SetMoney(ws.Cell(row, 6), r.CoverageLimit);
                SetMoney(ws.Cell(row, 7), r.EmployeeShare);
                SetMoney(ws.Cell(row, 8), r.EmployerShare);
                ws.Cell(row, 9).Value = r.StartDate?.ToString("MMM dd, yyyy") ?? "";
                ws.Cell(row, 10).Value = r.EndDate?.ToString("MMM dd, yyyy") ?? "";
                ws.Cell(row, 11).Value = r.AssignmentStatus;
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            OpenFile(path);
        }

        public static void ExportClaimsSummaryExcel(
            IEnumerable<VwClaimsSummary> data, string title)
        {
            var profile = GetCompanyProfile();
            var path = PickSavePath("Claims_Summary", "xlsx");
            if (path is null) return;
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Claims Summary");
            WriteExcelTitle(ws, title, profile);
            string[] h = {
                "Household/Resident ID","Full Name","Department","Barangay",
                "Total Claims","Total Claimed","Total Approved",
                "Total Released","Pending","Approved","Rejected"
            };
            WriteHeaders(ws, 5, h);
            int row = 6;
            foreach (var r in data)
            {
                ws.Cell(row, 1).Value = r.EmployeeNo;
                ws.Cell(row, 2).Value = r.FullName;
                ws.Cell(row, 3).Value = r.DeptName;
                ws.Cell(row, 4).Value = r.Barangay;
                ws.Cell(row, 5).Value = r.TotalClaims;
                SetMoney(ws.Cell(row, 6), r.TotalClaimed);
                SetMoney(ws.Cell(row, 7), r.TotalApproved);
                SetMoney(ws.Cell(row, 8), r.TotalReleased);
                ws.Cell(row, 9).Value = r.PendingClaims;
                ws.Cell(row, 10).Value = r.ApprovedClaims;
                ws.Cell(row, 11).Value = r.RejectedClaims;
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            OpenFile(path);
        }

        public static void ExportPremiumStatusExcel(
            IEnumerable<VwPremiumStatus> data, string title)
        {
            var profile = GetCompanyProfile();
            var path = PickSavePath("Premium_Status", "xlsx");
            if (path is null) return;
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Premium Status");
            WriteExcelTitle(ws, title, profile);
            string[] h = {
                "Employee","Policy","Billing Month","Due Date",
                "Total Amount","Amount Paid","Balance","Status"
            };
            WriteHeaders(ws, 5, h);
            int row = 6;
            foreach (var r in data)
            {
                ws.Cell(row, 1).Value = r.EmployeeName;
                ws.Cell(row, 2).Value = r.PolicyName;
                ws.Cell(row, 3).Value = r.BillingMonth.ToString("MMM yyyy");
                ws.Cell(row, 4).Value = r.DueDate.ToString("MMM dd, yyyy");
                SetMoney(ws.Cell(row, 5), r.TotalAmount);
                SetMoney(ws.Cell(row, 6), r.AmountPaid);
                SetMoney(ws.Cell(row, 7), r.Balance);
                ws.Cell(row, 8).Value = r.PaymentStatus;
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            OpenFile(path);
        }

        public static void ExportBenefitUtilizationExcel(
            IEnumerable<Benefit> data, string title)
        {
            var profile = GetCompanyProfile();
            var path = PickSavePath("Benefit_Utilization", "xlsx");
            if (path is null) return;
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Benefit Utilization");
            WriteExcelTitle(ws, title, profile);
            string[] h = {
                "Employee","Policy","Benefit Type","Year",
                "Maximum","Used","Remaining","Last Used"
            };
            WriteHeaders(ws, 5, h);
            int row = 6;
            foreach (var r in data)
            {
                ws.Cell(row, 1).Value = r.EmployeePolicy?.Employee?.FullName;
                ws.Cell(row, 2).Value = r.EmployeePolicy?.Policy?.PolicyName;
                ws.Cell(row, 3).Value = r.BenefitType;
                ws.Cell(row, 4).Value = r.YearPeriod;
                SetMoney(ws.Cell(row, 5), r.MaxBenefit);
                SetMoney(ws.Cell(row, 6), r.UsedBenefit);
                SetMoney(ws.Cell(row, 7), r.Remaining);
                ws.Cell(row, 8).Value = r.LastUsedDate?.ToString("MMM dd, yyyy") ?? "";
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            OpenFile(path);
        }

        // ══════════════════════════════════════════════════════════════
        // PDF
        // ══════════════════════════════════════════════════════════════

        public static void ExportEmployeeCoverageWord(
            IEnumerable<VwEmployeeCoverage> data, string title)
        {
            string[] headers = {
                "Household/Resident ID","Full Name","Department","Policy Name",
                "Policy Type","Coverage Limit","Emp Share","Employer Share",
                "Start Date","End Date","Status"
            };

            var rows = data.Select(r => new[] {
                r.EmployeeNo ?? "",
                r.FullName ?? "",
                r.DeptName ?? "",
                r.PolicyName ?? "",
                r.PolicyType ?? "",
                Money(r.CoverageLimit),
                Money(r.EmployeeShare),
                Money(r.EmployerShare),
                r.StartDate?.ToString("MMM dd, yyyy") ?? "",
                r.EndDate?.ToString("MMM dd, yyyy") ?? "",
                r.AssignmentStatus ?? ""
            });

            ExportWordTable("Employee_Coverage", title, headers, rows);
        }

        public static void ExportClaimsSummaryWord(
            IEnumerable<VwClaimsSummary> data, string title)
        {
            string[] headers = {
                "Household/Resident ID","Full Name","Department","Barangay",
                "Total Claims","Total Claimed","Total Approved",
                "Total Released","Pending","Approved","Rejected"
            };

            var rows = data.Select(r => new[] {
                r.EmployeeNo ?? "",
                r.FullName ?? "",
                r.DeptName ?? "",
                r.Barangay ?? "",
                r.TotalClaims.ToString(),
                Money(r.TotalClaimed),
                Money(r.TotalApproved),
                Money(r.TotalReleased),
                r.PendingClaims.ToString(),
                r.ApprovedClaims.ToString(),
                r.RejectedClaims.ToString()
            });

            ExportWordTable("Claims_Summary", title, headers, rows);
        }

        public static void ExportPremiumStatusWord(
            IEnumerable<VwPremiumStatus> data, string title)
        {
            string[] headers = {
                "Employee","Policy","Billing Month","Due Date",
                "Total Amount","Amount Paid","Balance","Status"
            };

            var rows = data.Select(r => new[] {
                r.EmployeeName ?? "",
                r.PolicyName ?? "",
                r.BillingMonth.ToString("MMM yyyy"),
                r.DueDate.ToString("MMM dd, yyyy"),
                Money(r.TotalAmount),
                Money(r.AmountPaid),
                Money(r.Balance),
                r.PaymentStatus ?? ""
            });

            ExportWordTable("Premium_Status", title, headers, rows);
        }

        public static void ExportBenefitUtilizationWord(
            IEnumerable<Benefit> data, string title)
        {
            string[] headers = {
                "Employee","Policy","Benefit Type","Year",
                "Maximum","Used","Remaining","Last Used"
            };

            var rows = data.Select(r => new[] {
                r.EmployeePolicy?.Employee?.FullName ?? "",
                r.EmployeePolicy?.Policy?.PolicyName ?? "",
                r.BenefitType,
                r.YearPeriod?.ToString() ?? "",
                Money(r.MaxBenefit),
                Money(r.UsedBenefit),
                Money(r.Remaining),
                r.LastUsedDate?.ToString("MMM dd, yyyy") ?? ""
            });

            ExportWordTable("Benefit_Utilization", title, headers, rows);
        }

        public static void ExportEmployeeCoveragePdf(
            IEnumerable<VwEmployeeCoverage> data, string title)
        {
            var profile = GetCompanyProfile();
            var path = PickSavePath("Employee_Coverage", "pdf");
            if (path is null) return;
            var list = data.ToList();

            QuestDocument.Create(c => c.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontSize(8));
                page.Header().Element(h => PdfHeader(h, title, profile));
                page.Footer().Element(f => PdfFooter(f, profile));
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cd => {
                        cd.RelativeColumn(2); cd.RelativeColumn(3);
                        cd.RelativeColumn(2); cd.RelativeColumn(3);
                        cd.RelativeColumn(2); cd.RelativeColumn(2);
                        cd.RelativeColumn(2); cd.RelativeColumn(2);
                        cd.RelativeColumn(2);
                    });
                    table.Header(th => {
                        foreach (var h in new[]{
                            "Household/Resident ID","Full Name","Department","Policy",
                            "Type","Coverage","Emp Share","Emp'r Share","Status"})
                            th.Cell().PdfHeaderCell(h);
                    });
                    bool alt = false;
                    foreach (var r in list)
                    {
                        var bg = alt ? "#F5F5F5" : "#FFFFFF";
                        table.Cell().PdfCell(r.EmployeeNo ?? "", bg);
                        table.Cell().PdfCell(r.FullName ?? "", bg);
                        table.Cell().PdfCell(r.DeptName ?? "", bg);
                        table.Cell().PdfCell(r.PolicyName ?? "", bg);
                        table.Cell().PdfCell(r.PolicyType ?? "", bg);
                        table.Cell().PdfCellRight($"₱{r.CoverageLimit:N0}", bg);
                        table.Cell().PdfCellRight($"₱{r.EmployeeShare:N0}", bg);
                        table.Cell().PdfCellRight($"₱{r.EmployerShare:N0}", bg);
                        table.Cell().PdfCell(r.AssignmentStatus ?? "", bg);
                        alt = !alt;
                    }
                });
            })).GeneratePdf(path);
            OpenFile(path);
        }

        public static void ExportClaimsSummaryPdf(
            IEnumerable<VwClaimsSummary> data, string title)
        {
            var profile = GetCompanyProfile();
            var path = PickSavePath("Claims_Summary", "pdf");
            if (path is null) return;
            var list = data.ToList();

            QuestDocument.Create(c => c.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontSize(8));
                page.Header().Element(h => PdfHeader(h, title, profile));
                page.Footer().Element(f => PdfFooter(f, profile));
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cd => {
                        cd.RelativeColumn(2); cd.RelativeColumn(3);
                        cd.RelativeColumn(2); cd.RelativeColumn(2);
                        cd.RelativeColumn(1);
                        cd.RelativeColumn(2); cd.RelativeColumn(2);
                        cd.RelativeColumn(2); cd.RelativeColumn(1);
                        cd.RelativeColumn(1); cd.RelativeColumn(1);
                    });
                    table.Header(th => {
                        foreach (var h in new[]{
                            "Household/Resident ID","Full Name","Department","Barangay","Claims",
                            "Claimed","Approved","Released",
                            "Pending","Approved","Rejected"})
                            th.Cell().PdfHeaderCell(h);
                    });
                    bool alt = false;
                    foreach (var r in list)
                    {
                        var bg = alt ? "#F5F5F5" : "#FFFFFF";
                        table.Cell().PdfCell(r.EmployeeNo ?? "", bg);
                        table.Cell().PdfCell(r.FullName ?? "", bg);
                        table.Cell().PdfCell(r.DeptName ?? "", bg);
                        table.Cell().PdfCell(r.Barangay ?? "", bg);
                        table.Cell().PdfCellRight(r.TotalClaims.ToString(), bg);
                        table.Cell().PdfCellRight($"₱{r.TotalClaimed:N0}", bg);
                        table.Cell().PdfCellRight($"₱{r.TotalApproved:N0}", bg);
                        table.Cell().PdfCellRight($"₱{r.TotalReleased:N0}", bg);
                        table.Cell().PdfCellRight(r.PendingClaims.ToString(), bg);
                        table.Cell().PdfCellRight(r.ApprovedClaims.ToString(), bg);
                        table.Cell().PdfCellRight(r.RejectedClaims.ToString(), bg);
                        alt = !alt;
                    }
                });
            })).GeneratePdf(path);
            OpenFile(path);
        }

        public static void ExportPremiumStatusPdf(
            IEnumerable<VwPremiumStatus> data, string title)
        {
            var profile = GetCompanyProfile();
            var path = PickSavePath("Premium_Status", "pdf");
            if (path is null) return;
            var list = data.ToList();

            QuestDocument.Create(c => c.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontSize(8));
                page.Header().Element(h => PdfHeader(h, title, profile));
                page.Footer().Element(f => PdfFooter(f, profile));
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cd => {
                        cd.RelativeColumn(3); cd.RelativeColumn(3);
                        cd.RelativeColumn(2); cd.RelativeColumn(2);
                        cd.RelativeColumn(2); cd.RelativeColumn(2);
                        cd.RelativeColumn(2); cd.RelativeColumn(2);
                    });
                    table.Header(th => {
                        foreach (var h in new[]{
                            "Employee","Policy","Billing Month","Due Date",
                            "Total Due","Paid","Balance","Status"})
                            th.Cell().PdfHeaderCell(h);
                    });
                    bool alt = false;
                    foreach (var r in list)
                    {
                        var bg = alt ? "#F5F5F5" : "#FFFFFF";
                        table.Cell().PdfCell(r.EmployeeName ?? "", bg);
                        table.Cell().PdfCell(r.PolicyName ?? "", bg);
                        table.Cell().PdfCell(r.BillingMonth.ToString("MMM yyyy"), bg);
                        table.Cell().PdfCell(r.DueDate.ToString("MMM dd, yyyy"), bg);
                        table.Cell().PdfCellRight($"₱{r.TotalAmount:N2}", bg);
                        table.Cell().PdfCellRight($"₱{r.AmountPaid:N2}", bg);
                        table.Cell().PdfCellRight($"₱{r.Balance:N2}", bg);
                        table.Cell().PdfCell(r.PaymentStatus ?? "", bg);
                        alt = !alt;
                    }
                });
            })).GeneratePdf(path);
            OpenFile(path);
        }

        public static void ExportBenefitUtilizationPdf(
            IEnumerable<Benefit> data, string title)
        {
            var profile = GetCompanyProfile();
            var path = PickSavePath("Benefit_Utilization", "pdf");
            if (path is null) return;
            var list = data.ToList();

            QuestDocument.Create(c => c.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontSize(8));
                page.Header().Element(h => PdfHeader(h, title, profile));
                page.Footer().Element(f => PdfFooter(f, profile));
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cd => {
                        cd.RelativeColumn(3); cd.RelativeColumn(3);
                        cd.RelativeColumn(2); cd.RelativeColumn(1);
                        cd.RelativeColumn(2); cd.RelativeColumn(2);
                        cd.RelativeColumn(2); cd.RelativeColumn(2);
                    });
                    table.Header(th => {
                        foreach (var h in new[]{
                            "Employee","Policy","Benefit Type","Year",
                            "Maximum","Used","Remaining","Last Used"})
                            th.Cell().PdfHeaderCell(h);
                    });
                    bool alt = false;
                    foreach (var r in list)
                    {
                        var bg = alt ? "#F5F5F5" : "#FFFFFF";
                        table.Cell().PdfCell(r.EmployeePolicy?.Employee?.FullName ?? "", bg);
                        table.Cell().PdfCell(r.EmployeePolicy?.Policy?.PolicyName ?? "", bg);
                        table.Cell().PdfCell(r.BenefitType, bg);
                        table.Cell().PdfCellRight(r.YearPeriod?.ToString() ?? "", bg);
                        table.Cell().PdfCellRight($"₱{r.MaxBenefit:N2}", bg);
                        table.Cell().PdfCellRight($"₱{r.UsedBenefit:N2}", bg);
                        table.Cell().PdfCellRight($"₱{r.Remaining:N2}", bg);
                        table.Cell().PdfCell(r.LastUsedDate?.ToString("MMM dd, yyyy") ?? "", bg);
                        alt = !alt;
                    }
                });
            })).GeneratePdf(path);
            OpenFile(path);
        }

        // ══════════════════════════════════════════════════════════════
        // PDF HELPERS
        // ══════════════════════════════════════════════════════════════

        private static void PdfHeader(IContainer c, string reportTitle, CompanyProfile profile)
        {
            var sealPath = GetAssetPath(SulopSealFile);
            var appLogoPath = GetAssetPath(AppLogoFile);

            c.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.ConstantItem(70).Height(58).AlignMiddle().AlignLeft().Element(img =>
                    {
                        if (sealPath is not null)
                            img.Image(sealPath).FitArea();
                    });

                    row.RelativeItem().AlignCenter().Column(inner =>
                    {
                        inner.Item().AlignCenter().Text(MunicipalityName)
                            .Bold().FontSize(13)
                            .FontColor(Color.FromHex("#0F172A"));
                        inner.Item().AlignCenter().Text(profile.Name ?? "Municipality of Sulop")
                            .FontSize(8)
                            .FontColor(Color.FromHex("#475569"));
                        inner.Item().AlignCenter().Text(SysName)
                            .Italic().FontSize(7)
                            .FontColor(Color.FromHex("#64748B"));
                        inner.Item().PaddingTop(5).AlignCenter().Text(reportTitle)
                            .Bold().FontSize(10)
                            .FontColor(Color.FromHex("#0F172A"));
                        inner.Item().AlignCenter().Text($"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                            .FontSize(7)
                            .FontColor(Color.FromHex("#64748B"));
                    });

                    row.ConstantItem(70).Height(58).AlignMiddle().AlignRight().Element(img =>
                    {
                        if (appLogoPath is not null)
                            img.Image(appLogoPath).FitArea();
                    });
                });
                col.Item().PaddingTop(8).PaddingBottom(8)
                   .LineHorizontal(1).LineColor(Color.FromHex("#CBD5E1"));
            });
        }

        private static void PdfFooter(IContainer c, CompanyProfile profile)
        {
            c.Row(row =>
            {
                row.RelativeItem()
                   .Text($"{profile.Name ?? "Municipality of Sulop"} — {SysName}")
                   .FontSize(7).FontColor(Color.FromHex("#9E9E9E"));
                row.ConstantItem(80).AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(7).FontColor(Color.FromHex("#9E9E9E"));
                    x.CurrentPageNumber().FontSize(7).FontColor(Color.FromHex("#9E9E9E"));
                    x.Span(" of ").FontSize(7).FontColor(Color.FromHex("#9E9E9E"));
                    x.TotalPages().FontSize(7).FontColor(Color.FromHex("#9E9E9E"));
                });
            });
        }

        // ══════════════════════════════════════════════════════════════
        // SHARED HELPERS
        // ══════════════════════════════════════════════════════════════

        private static void WriteExcelTitle(IXLWorksheet ws, string title, CompanyProfile profile)
        {
            ws.Row(1).Height = 28;
            ws.Row(2).Height = 18;
            ws.Row(3).Height = 18;
            ws.Row(4).Height = 18;

            ws.Range(1, 2, 1, 10).Merge();
            ws.Range(2, 2, 2, 10).Merge();
            ws.Range(3, 2, 3, 10).Merge();
            ws.Range(4, 2, 4, 10).Merge();

            ws.Cell(1, 2).Value = MunicipalityName;
            ws.Cell(1, 2).Style.Font.Bold = true;
            ws.Cell(1, 2).Style.Font.FontSize = 14;
            ws.Cell(1, 2).Style.Font.FontColor = XLColor.FromHtml("#0F172A");
            ws.Cell(1, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 2).Value = profile.Name ?? "Municipality of Sulop";
            ws.Cell(2, 2).Style.Font.FontSize = 9;
            ws.Cell(2, 2).Style.Font.FontColor = XLColor.FromHtml("#475569");
            ws.Cell(2, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(3, 2).Value = title;
            ws.Cell(3, 2).Style.Font.Bold = true;
            ws.Cell(3, 2).Style.Font.FontSize = 10;
            ws.Cell(3, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(4, 2).Value = $"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}";
            ws.Cell(4, 2).Style.Font.FontColor = XLColor.Gray;
            ws.Cell(4, 2).Style.Font.FontSize = 9;
            ws.Cell(4, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var sealPath = GetAssetPath(SulopSealFile);
            if (sealPath is not null)
                ws.AddPicture(sealPath).MoveTo(ws.Cell(1, 1)).WithSize(58, 58);

            var appLogoPath = GetAssetPath(AppLogoFile);
            if (appLogoPath is not null)
                ws.AddPicture(appLogoPath).MoveTo(ws.Cell(1, 11)).WithSize(58, 58);

            ws.Range(4, 1, 4, 11).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Range(4, 1, 4, 11).Style.Border.BottomBorderColor = XLColor.FromHtml("#CBD5E1");
        }

        private static void ExportWordTable(
            string baseName,
            string title,
            string[] headers,
            IEnumerable<string[]> rows)
        {
            var profile = GetCompanyProfile();
            var path = PickSavePath(baseName, "doc");
            if (path is null) return;

            File.WriteAllText(path, BuildWordHtml(title, profile, headers, rows), Encoding.UTF8);
            OpenFile(path);
        }

        private static string BuildWordHtml(
            string title,
            CompanyProfile profile,
            string[] headers,
            IEnumerable<string[]> rows)
        {
            var seal = ImageDataUri(GetAssetPath(SulopSealFile));
            var appLogo = ImageDataUri(GetAssetPath(AppLogoFile));
            var sb = new StringBuilder();

            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"utf-8\">");
            sb.AppendLine("<style>");
            sb.AppendLine("@page { size: landscape; margin: 0.55in; }");
            sb.AppendLine("body { font-family: Calibri, Arial, sans-serif; color: #111827; }");
            sb.AppendLine(".header { width: 100%; border-collapse: collapse; margin-bottom: 14px; }");
            sb.AppendLine(".logo { width: 88px; text-align: center; vertical-align: middle; }");
            sb.AppendLine(".logo img { max-width: 70px; max-height: 70px; }");
            sb.AppendLine(".title { text-align: center; vertical-align: middle; }");
            sb.AppendLine(".mun { font-size: 20px; font-weight: 700; letter-spacing: .3px; }");
            sb.AppendLine(".sub { font-size: 11px; color: #4b5563; font-style: italic; }");
            sb.AppendLine(".report { font-size: 15px; font-weight: 700; margin-top: 12px; text-transform: uppercase; }");
            sb.AppendLine(".date { font-size: 11px; color: #4b5563; }");
            sb.AppendLine(".rule { border-top: 1px solid #cbd5e1; margin: 4px 0 14px 0; }");
            sb.AppendLine("table.data { width: 100%; border-collapse: collapse; font-size: 10.5px; }");
            sb.AppendLine("table.data th { background: #1565c0; color: white; padding: 6px; border: 1px solid #d1d5db; text-align: left; }");
            sb.AppendLine("table.data td { padding: 5px; border: 1px solid #d1d5db; }");
            sb.AppendLine("table.data tr:nth-child(even) td { background: #f8fafc; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<table class=\"header\"><tr>");
            sb.AppendLine($"<td class=\"logo\">{LogoHtml(seal)}</td>");
            sb.AppendLine("<td class=\"title\">");
            sb.AppendLine($"<div class=\"mun\">{Html(MunicipalityName)}</div>");
            sb.AppendLine("<div class=\"sub\">Municipality of Sulop</div>");
            sb.AppendLine("<div class=\"sub\">Sulop, Davao Occidental</div>");
            sb.AppendLine($"<div class=\"sub\">{Html(SysName)}</div>");
            sb.AppendLine($"<div class=\"report\">{Html(title)}</div>");
            sb.AppendLine($"<div class=\"date\">Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}</div>");
            sb.AppendLine("</td>");
            sb.AppendLine($"<td class=\"logo\">{LogoHtml(appLogo)}</td>");
            sb.AppendLine("</tr></table>");
            sb.AppendLine("<div class=\"rule\"></div>");
            sb.AppendLine("<table class=\"data\"><thead><tr>");
            foreach (var h in headers)
                sb.AppendLine($"<th>{Html(h)}</th>");
            sb.AppendLine("</tr></thead><tbody>");
            foreach (var row in rows)
            {
                sb.AppendLine("<tr>");
                foreach (var value in row)
                    sb.AppendLine($"<td>{Html(value)}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        private static string LogoHtml(string? dataUri)
        {
            return string.IsNullOrWhiteSpace(dataUri)
                ? "&nbsp;"
                : $"<img src=\"{dataUri}\" alt=\"Logo\">";
        }

        private static string? ImageDataUri(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            var mime = ext is "jpg" or "jpeg" ? "image/jpeg" : "image/png";
            return $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
        }

        private static string Html(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string Money(decimal value)
        {
            return $"PHP {value:N2}";
        }

        private static void WriteHeaders(IXLWorksheet ws, int row, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(row, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorderColor = XLColor.White;
            }
        }

        private static void SetMoney(IXLCell cell, decimal value)
        {
            cell.Value = (double)value;
            cell.Style.NumberFormat.Format = "#,##0.00";
        }

        private static string? PickSavePath(string baseName, string ext)
        {
            var dlg = new SaveFileDialog
            {
                FileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}.{ext}",
                DefaultExt = $".{ext}",
                Filter = ext switch
                {
                    "pdf" => "PDF Files|*.pdf",
                    "doc" => "Word Documents|*.doc",
                    _ => "Excel Files|*.xlsx"
                }
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        private static void OpenFile(string path)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                App.ReportError("Open Exported Report Failed", ex, showMessage: false);
            }
        }
    }

    // ── QuestPDF Extension Methods ─────────────────────────────────────
    internal static class PdfExtensions
    {
        public static void PdfHeaderCell(this IContainer c, string text)
        {
            c.Background(Color.FromHex("#1565C0"))
             .Padding(5)
             .Text(text)
             .Bold().FontSize(8).FontColor(Colors.White);
        }

        public static void PdfCell(this IContainer c, string text, string bgHex)
        {
            c.Background(Color.FromHex(bgHex))
             .BorderBottom(0.3f).BorderColor(Color.FromHex("#E0E0E0"))
             .Padding(4)
             .Text(text).FontSize(8);
        }

        public static void PdfCellRight(this IContainer c, string text, string bgHex)
        {
            c.Background(Color.FromHex(bgHex))
             .BorderBottom(0.3f).BorderColor(Color.FromHex("#E0E0E0"))
             .Padding(4).AlignRight()
             .Text(text).FontSize(8);
        }
    }
}
