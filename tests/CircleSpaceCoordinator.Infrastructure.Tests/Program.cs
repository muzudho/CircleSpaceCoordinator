namespace CircleSpaceCoordinator.Infrastructure.Tests;

using ClosedXML.Excel;
using System.Text.Json;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Infrastructure.Json;
using CircleSpaceCoordinator.Infrastructure.Tabular;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--validate-project")
        {
            var project = ProjectJsonSerializer.Load(File.ReadAllText(args[1]));
            var results = ProjectEvaluator.Evaluate(project);
            Console.WriteLine($"Project JSON valid: participants={project.Participants.Count}; plans={project.Plans.Count}; results={results.Count}");
            return 0;
        }

        if (args.Length != 0)
        {
            Console.Error.WriteLine("Usage: CircleSpaceCoordinator.Infrastructure.Tests [--validate-project path]");
            return 2;
        }

        var tests = new (string Name, Action Run)[]
        {
            ("Portable fixtures retain local definitions and reject missing knowledge coefficients", PortableFixtures),
            ("Channel column mappings and imported values survive JSON round trip", ChannelRoundTrip),
            ("Channel imports preserve Excel numeric precision and formatted circle IDs", ChannelNumericPrecision),
            ("Anonymous version-1 example loads and evaluates", ExampleLoadsAndEvaluates),
            ("Project and evaluation results survive JSON round trip", ProjectRoundTrip),
            ("Project-wide editor view survives JSON round trip", EditorViewRoundTrip),
            ("Unknown JSON properties are rejected", UnknownPropertyIsRejected),
            ("Legacy participants use their internal ID as circle ID", LegacyParticipantGetsCircleId),
            ("Participant CSV headers are guessed and mapped", ParticipantCsvIsMapped),
            ("CSV encoding can be corrected from UTF-8 to CP932 without changing the source", ParticipantCsvEncodingCanBeCorrected),
            ("CSV preview encoding mismatch never throws a decoder exception", ParticipantCsvPreviewDoesNotThrow),
            ("UTF-8 CSV supports BOM and no BOM with quoted Japanese fields", ParticipantUtf8CsvVariants),
            ("Participant XLSX sheets are read without Microsoft Excel", ParticipantXlsxIsRead),
            ("Confidential flag and genre survive JSON round trip", ConfidentialGenreRoundTrip),
            ("Island topology survives JSON round trip", IslandTopologyRoundTrip),
            ("Seat labels survive JSON round trip", SeatLabelsRoundTrip),
            ("Desk numbers survive JSON round trip", DeskNumbersRoundTrip),
            ("Circle seat values are written to selected Excel columns", CircleSeatValuesAreExported),
            ("CSV export preserves encoding, quoted cells and independent input files", CircleSeatCsvExport),
            ("New XLSX and CSV export match preview and refuse to overwrite existing files", NewCircleSeatFiles),
            ("Existing XLSX and CSV can append number columns without changing unrelated cells", AppendCircleSeatColumns),
        };

        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"PASS: {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL: {test.Name}");
                Console.Error.WriteLine(exception.Message);
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void PortableFixtures()
    {
        var layouts = ProjectPortableSerializer.Load(File.ReadAllText("examples/fictional-proposals.project-portable.json"));
        AssertEqual(2, layouts.Projects.Length);
        AssertEqual(layouts.Projects[0].DeskTypes[0].Id, layouts.Projects[1].DeskTypes[0].Id);
        AssertEqual("Rule A", layouts.Projects[0].DeskLayouts[0].Definitions!.Requests[0].Description);
        AssertEqual("Rule C", layouts.Projects[1].DeskLayouts[0].Definitions!.Requests[0].Description);
        var json = File.ReadAllText("examples/fictional-books.project-portable.json");
        var knowledge = ProjectPortableSerializer.Load(json).Document.Knowledge!.Single();
        AssertEqual(true, knowledge.IsConfidential);
        AssertEqual(1d, knowledge.Cells[0].Weight);
        foreach (var field in new[] { "scale", "offset", "overallWeight", "isConfidential", "inputRule", "defaultWeight" })
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            node["knowledge"]![0]!.AsObject().Remove(field);
            var rejected = false;
            try { ProjectPortableSerializer.Load(node.ToJsonString()); } catch (JsonException) { rejected = true; }
            AssertEqual(true, rejected);
        }
    }

    private static void ExampleLoadsAndEvaluates()
    {
        var project = ProjectJsonSerializer.Load(ReadExample());
        var results = ProjectEvaluator.Evaluate(project).ToDictionary(result => result.PlanId);

        AssertEqual("sample-event", project.Id);
        AssertEqual(2, project.Plans.Count);
        AssertNear(13d, results["plan-0"].TotalScore);
        AssertNear(6.5d, results["plan-1"].TotalScore);
    }

    private static void ProjectRoundTrip()
    {
        var project = ProjectJsonSerializer.Load(ReadExample());
        var results = ProjectEvaluator.Evaluate(project);
        var saved = ProjectJsonSerializer.Save(project, results);
        var reloaded = ProjectJsonSerializer.Load(saved);

        AssertEqual(project.Id, reloaded.Id);
        AssertEqual(project.Venue.Zones.Count, reloaded.Venue.Zones.Count);
        AssertEqual(project.Plans.Count, reloaded.Plans.Count);
        AssertEqual(project.Participants[0].RequiredCellCount, reloaded.Participants[0].RequiredCellCount);

        using var document = JsonDocument.Parse(saved);
        AssertEqual(2, document.RootElement.GetProperty("evaluationResults").GetArrayLength());
    }

    private static void IslandTopologyRoundTrip()
    {
        var source = ProjectJsonSerializer.Load(ReadExample());
        var plan = source.Plans[0];
        var deskIds = plan.DeskPlacements.Take(2).Select(item => item.Id).ToArray();
        var project = source with
        {
            Plans = [plan with
            {
                IslandConnectors = [new IslandConnector("link-1", deskIds[0], deskIds[1], new(0, 0), new(1, 0))],
                DisabledIslandConnections = [new DisabledIslandConnection(new(0, 0), new(1, 0))],
                FacingRegions = [new FacingRegion("facing-1", new(0, 0), new(1, 1))],
            }, .. source.Plans.Skip(1)],
        };

        var reloaded = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(project));
        AssertEqual("link-1", reloaded.Plans[0].IslandConnectors.Single().Id);
        AssertEqual(deskIds[1], reloaded.Plans[0].IslandConnectors.Single().SecondDeskId);
        AssertEqual(new CircleSpaceCoordinator.Core.Geometry.GridPosition(1, 0), reloaded.Plans[0].IslandConnectors.Single().SecondCell!.Value);
        AssertEqual(new CircleSpaceCoordinator.Core.Geometry.GridPosition(1, 0), reloaded.Plans[0].DisabledIslandConnections.Single().SecondCell);
        AssertEqual(new CircleSpaceCoordinator.Core.Geometry.GridPosition(1, 1), reloaded.Plans[0].FacingRegions.Single().SecondCorner);
    }

    private static void SeatLabelsRoundTrip()
    {
        var source = ProjectJsonSerializer.Load(ReadExample());
        var plan = source.Plans[0];
        var project = source with
        {
            Plans = [plan with
            {
                SeatLabels = [new DeskSeatLabel(plan.DeskPlacements[0].Id, new(0, 0), "ア", "10左")],
            }, .. source.Plans.Skip(1)],
        };

        var reloaded = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(project));
        var label = reloaded.Plans[0].SeatLabels.Single();
        AssertEqual("ア", label.BlockName);
        AssertEqual("10左", label.SeatName);
        AssertEqual(plan.DeskPlacements[0].Id, label.DeskPlacementId);
    }

    private static void DeskNumbersRoundTrip()
    {
        var source = ProjectJsonSerializer.Load(ReadExample());
        var plan = source.Plans[0];
        var project = source with
        {
            Plans = [plan with
            {
                DeskPlacements = [plan.DeskPlacements[0] with { DeskNumber = "机-12" }, .. plan.DeskPlacements.Skip(1)],
            }, .. source.Plans.Skip(1)],
        };

        var reloaded = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(project));
        if (reloaded.Plans[0].DeskPlacements[0].DeskNumber != "机-12")
            throw new InvalidOperationException("Desk number did not survive the JSON round trip.");
    }

    private static void UnknownPropertyIsRejected()
    {
        const string invalid = """
            {
              "schemaVersion": "1.0",
              "unknownProperty": true
            }
            """;

        try
        {
            ProjectJsonSerializer.Load(invalid);
        }
        catch (JsonException)
        {
            return;
        }

        throw new InvalidOperationException("An unknown property was accepted.");
    }

    private static void EditorViewRoundTrip()
    {
        var project = ProjectJsonSerializer.Load(ReadExample()) with
        {
            EditorView = new EditorViewState(1.5d, -120d, 84d),
        };
        var saved = ProjectJsonSerializer.Save(project);
        var reloaded = ProjectJsonSerializer.Load(saved);

        AssertEqual(project.EditorView!, reloaded.EditorView!);
        AssertEqual(2, reloaded.Plans.Count);
    }

    private static void ConfidentialGenreRoundTrip()
    {
        var project = ProjectJsonSerializer.Load(ReadExample()) with
        {
            IsConfidential = true,
            GenreStyles = [new GenreStyleDefinition("action", "red", "white", "dots")],
        };
        var saved = ProjectJsonSerializer.Save(project);
        var reloaded = ProjectJsonSerializer.Load(saved);

        AssertEqual(true, reloaded.IsConfidential);
        AssertEqual("action", reloaded.Participants[0].GenreId!);
        AssertEqual(new GenreStyleDefinition("action", "red", "white", "dots"), reloaded.GenreStyles.Single());
        using var document = JsonDocument.Parse(saved);
        AssertEqual(true, document.RootElement.GetProperty("project").GetProperty("isConfidential").GetBoolean());
    }

    private static void LegacyParticipantGetsCircleId()
    {
        var project = ProjectJsonSerializer.Load(ReadExample());
        AssertEqual(project.Participants[0].Id, project.Participants[0].CircleId);
        var saved = ProjectJsonSerializer.Save(project);
        using var document = JsonDocument.Parse(saved);
        AssertEqual(project.Participants[0].Id,
            document.RootElement.GetProperty("participants")[0].GetProperty("circleId").GetString()!);
    }

    private static void ParticipantCsvIsMapped()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "participants.csv");
        try
        {
            File.WriteAllText(path,
                "サークルID,サークル名,必要セル数,合体先サークルID,ジャンルID\nABC1234,ぐれーすけーる,2,XYZ9876,11\nXYZ9876,架空の合体先,1,ABC1234,12\n",
                System.Text.Encoding.UTF8);
            var sheet = ParticipantTableReader.Read(path).Single();
            var rows = ParticipantTableMapper.Map(sheet, ParticipantTableMapper.Guess(sheet.Headers));
            AssertEqual(2, rows.Count);
            AssertEqual("ABC1234", rows[0].CircleId);
            AssertEqual("ぐれーすけーる", rows[0].DisplayName);
            AssertEqual(2, rows[0].RequiredCellCount);
            AssertEqual("XYZ9876", rows[0].CombinedWithCircleId!);
            AssertEqual("11", rows[0].GenreId!);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void ParticipantCsvEncodingCanBeCorrected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"circle-cp932-{Guid.NewGuid():N}.csv");
        // CP932 fixture: ID, name / 001, "日本,①". Includes a Windows extension character.
        byte[] original = [0x49, 0x44, 0x2C, 0x6E, 0x61, 0x6D, 0x65, 0x0D, 0x0A,
            0x30, 0x30, 0x31, 0x2C, 0x22, 0x93, 0xFA, 0x96, 0x7B, 0x2C, 0x87, 0x40, 0x22, 0x0D, 0x0A];
        try
        {
            File.WriteAllBytes(path, original);
            var rejected = false;
            try { ParticipantTableReader.Read(path); }
            catch (System.Text.DecoderFallbackException) { rejected = true; }
            AssertEqual(true, rejected);
            var sheet = ParticipantTableReader.Read(path, ParticipantCsvEncoding.ShiftJis).Single();
            var rows = ParticipantTableMapper.Map(sheet, new(0, 1));
            AssertEqual(1, rows.Count);
            AssertEqual("001", rows[0].CircleId);
            AssertEqual("日本,①", rows[0].DisplayName);
            AssertEqual(true, original.SequenceEqual(File.ReadAllBytes(path)));
            // An incomplete CP932 lead byte must not silently become a replacement character.
            File.WriteAllBytes(path, [0x49, 0x44, 0x2C, 0x6E, 0x61, 0x6D, 0x65, 0x0A, 0x31, 0x2C, 0x81]);
            rejected = false;
            try { ParticipantTableReader.Read(path, ParticipantCsvEncoding.ShiftJis); }
            catch (System.Text.DecoderFallbackException) { rejected = true; }
            AssertEqual(true, rejected);
        }
        finally { File.Delete(path); }
    }

    private static void ParticipantCsvPreviewDoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"circle-preview-{Guid.NewGuid():N}.csv");
        var decoderExceptions = 0;
        void OnException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs args)
        {
            if (args.Exception is System.Text.DecoderFallbackException)
                decoderExceptions++;
        }
        AppDomain.CurrentDomain.FirstChanceException += OnException;
        try
        {
            // The reported 0x8B byte must become an ordinary mismatch result.
            File.WriteAllBytes(path, [0x8B]);
            AssertEqual(false, ParticipantTableReader.TryReadCsv(path, ParticipantCsvEncoding.Utf8, out var invalid));
            AssertEqual(true, invalid is null);
            AssertEqual(false, ParticipantTableReader.TryReadCsv(path, ParticipantCsvEncoding.ShiftJis, out invalid));
            AssertEqual(true, invalid is null);

            byte[] original = [0x49, 0x44, 0x2C, 0x6E, 0x61, 0x6D, 0x65, 0x0A,
                0x31, 0x2C, 0x93, 0xFA, 0x96, 0x7B, 0x0A];
            File.WriteAllBytes(path, original);
            var garbled = ParticipantTableReader.ReadCsvPreview(path, ParticipantCsvEncoding.Utf8);
            AssertEqual(true, garbled.HasDecodingErrors);
            AssertEqual("ID", garbled.Sheet.Headers[0]);
            AssertEqual("1", garbled.Sheet.Rows[0][0]);
            AssertEqual(true, garbled.Sheet.Rows[0][1].Contains('\uFFFD'));
            var readable = ParticipantTableReader.ReadCsvPreview(path, ParticipantCsvEncoding.ShiftJis);
            AssertEqual(false, readable.HasDecodingErrors);
            AssertEqual("日本", readable.Sheet.Rows[0][1]);
            AssertEqual(false, ParticipantTableReader.TryReadCsv(path, ParticipantCsvEncoding.Utf8, out _));
            AssertEqual(true, ParticipantTableReader.TryReadCsv(path, ParticipantCsvEncoding.ShiftJis, out var corrected));
            AssertEqual("日本", corrected!.Rows[0][1]);
            AssertEqual(true, original.SequenceEqual(File.ReadAllBytes(path)));

            // A literal replacement character in a valid UTF-8 file is not a decode failure.
            File.WriteAllText(path, "ID,name\n1,日本\uFFFD\n", new System.Text.UTF8Encoding(true));
            AssertEqual(true, ParticipantTableReader.TryReadCsv(path, ParticipantCsvEncoding.Utf8, out var valid));
            AssertEqual("日本\uFFFD", valid!.Rows[0][1]);
            AssertEqual(0, decoderExceptions);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnException;
            File.Delete(path);
        }
    }

    private static void ParticipantUtf8CsvVariants()
    {
        var path = Path.Combine(Path.GetTempPath(), $"circle-utf8-{Guid.NewGuid():N}.csv");
        try
        {
            foreach (var bom in new[] { false, true })
            {
                File.WriteAllText(path, "サークルID,サークル名,備考\r\n001,\"日本,①\",\"一行目\r\n二行目\"\r\n",
                    new System.Text.UTF8Encoding(bom));
                var sheet = ParticipantTableReader.Read(path, ParticipantCsvEncoding.Utf8).Single();
                AssertEqual("サークルID", sheet.Headers[0]);
                var rows = ParticipantTableMapper.Map(sheet, ParticipantTableMapper.Guess(sheet.Headers));
                AssertEqual("001", rows.Single().CircleId);
                AssertEqual("日本,①", rows.Single().DisplayName);
                AssertEqual("一行目\r\n二行目", sheet.Rows[0][2]);
                var bytes = File.ReadAllBytes(path).Concat(new byte[] { 0xFF }).ToArray();
                File.WriteAllBytes(path, bytes);
                var rejected = false;
                try { ParticipantTableReader.Read(path); }
                catch (System.Text.DecoderFallbackException) { rejected = true; }
                AssertEqual(true, rejected);
            }
        }
        finally { File.Delete(path); }
    }

    private static void ParticipantXlsxIsRead()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-xlsx-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "participants.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.AddWorksheet("参加サークル一覧");
                worksheet.Cell(1, 1).Value = "サークルID";
                worksheet.Cell(1, 2).Value = "サークル名";
                worksheet.Cell(2, 1).Value = "ABC1234";
                worksheet.Cell(2, 2).Value = "ぐれーすけーる";
                workbook.SaveAs(path);
            }

            var sheet = ParticipantTableReader.Read(path).Single();
            AssertEqual("参加サークル一覧", sheet.Name);
            AssertEqual("サークルID", sheet.Headers[0]);
            AssertEqual("ぐれーすけーる", sheet.Rows[0][1]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AppendCircleSeatColumns()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"append-circle-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var extension in new[] { ".xlsx", ".csv" })
            {
                var path = Path.Combine(directory, "target" + extension);
                if (extension == ".csv") File.WriteAllText(path, "ID,メモ\r\n001,残す\r\n002,そのまま\r\n", new System.Text.UTF8Encoding(true));
                else
                {
                    using var book = new XLWorkbook();
                    var sheet = book.AddWorksheet("target");
                    sheet.Cell(2, 2).Value = "ID";
                    sheet.Cell(2, 3).Value = "メモ";
                    sheet.Cell(3, 2).Value = "001";
                    sheet.Cell(3, 3).Value = "残す";
                    sheet.Cell(3, 3).Style.Fill.BackgroundColor = XLColor.Red;
                    sheet.Cell(4, 2).Value = "002";
                    sheet.Cell(4, 3).Value = "そのまま";
                    book.AddWorksheet("別シート").Cell(1, 1).FormulaA1 = "1+2";
                    book.SaveAs(path);
                }
                var result = CircleSeatExcelExporter.Export(path, "target", 2, 3, 0, [new("001", "B", "#UNDEFINED_SPACE_NUMBER")],
                    outputHeaders: ["ID", "メモ", "ブロック番号", "セル番号"]);
                AssertEqual(1, result.UpdatedRowCount);
                var loaded = ParticipantTableReader.Read(path).First();
                AssertEqual("セル番号", loaded.Headers[3]);
                AssertEqual("#UNDEFINED_SPACE_NUMBER", loaded.Rows[0][3]);
                AssertEqual("そのまま", loaded.Rows[1][1]);
                AssertEqual("", loaded.Rows[1][3]);
                if (extension == ".xlsx")
                {
                    using var book = new XLWorkbook(path);
                    AssertEqual(XLColor.Red, book.Worksheet("target").Cell(3, 3).Style.Fill.BackgroundColor);
                    AssertEqual("1+2", book.Worksheet("別シート").Cell(1, 1).FormulaA1);
                }
                var before = File.ReadAllBytes(path);
                try
                {
                    CircleSeatExcelExporter.Export(path, "target", 2, 3, 0, [], outputHeaders: ["違うID", "メモ", "ブロック番号", "セル番号"]);
                    throw new Exception("Changed header must be rejected.");
                }
                catch (InvalidOperationException) { }
                AssertEqual(true, before.SequenceEqual(File.ReadAllBytes(path)));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void NewCircleSeatFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"new-circle-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var source = new ParticipantTableSheet("元の表", ["ID", "メモ", "ブロック", "セル番"],
                [new[] { "001", "カンマ,と\"引用\"\n改行", "旧", "旧番号" }, new[] { "002", "=文字列", "保持", "保持番号" }]);
            var prepared = CircleSeatTableWriter.Prepare(source, 2, 3, 0, [new("001", "ア", "03左"), new("999", "イ", "04右")]);
            AssertEqual("旧", source.Rows[0][2]);
            AssertEqual(1, prepared.Result.UpdatedRowCount);
            AssertEqual(1, prepared.Result.MissingCircleCount);
            foreach (var extension in new[] { ".xlsx", ".csv" })
            {
                var path = Path.Combine(directory, "new" + extension);
                CircleSeatTableWriter.Create(path, prepared.Sheet);
                var actual = ParticipantTableReader.Read(path).Single();
                AssertEqual(string.Join("|", source.Headers), string.Join("|", actual.Headers));
                for (var row = 0; row < actual.Rows.Count; row++)
                    for (var column = 0; column < actual.Headers.Count; column++)
                        AssertEqual(prepared.Sheet.Rows[row][column], actual.Rows[row][column]);
                var original = File.ReadAllBytes(path);
                try { CircleSeatTableWriter.Create(path, source); throw new Exception("Existing file must be rejected."); }
                catch (IOException) { }
                AssertEqual(true, original.SequenceEqual(File.ReadAllBytes(path)));
            }
            try { CircleSeatTableWriter.Prepare(source, 0, 0, 1, []); throw new Exception("Overlapping columns must be rejected."); }
            catch (InvalidOperationException) { }
            try { CircleSeatTableWriter.Prepare(source, -1, 3, 0, []); throw new Exception("Missing columns must be rejected."); }
            catch (InvalidOperationException) { }
            AssertEqual(2, Directory.GetFiles(directory).Length);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CircleSeatCsvExport()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-csv-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var variant in new[] { 0, 1, 2 })
            {
                var encoding = variant == 2
                    ? System.Text.CodePagesEncodingProvider.Instance.GetEncoding(932)!
                    : new System.Text.UTF8Encoding(variant == 1, true);
                var csvEncoding = variant == 2 ? ParticipantCsvEncoding.ShiftJis : ParticipantCsvEncoding.Utf8;
                var input = Path.Combine(directory, "input.csv");
                var output = Path.Combine(directory, "output.csv");
                File.WriteAllText(input, "ID,ブロック,席,メモ\r\n001,旧,旧,\"引用\"\"符,と\r\n改行\"\r\n002,残す,残す,そのまま\r\n", encoding);
                var original = File.ReadAllBytes(input);
                File.Copy(input, output, true);
                var result = CircleSeatExcelExporter.Export(output, "output", 1, 2, 0,
                    [new("001", "ア", "10左"), new("999", "イ", "20右")], csvEncoding);
                AssertEqual(1, result.UpdatedRowCount);
                AssertEqual(1, result.MissingCircleCount);
                AssertEqual(true, original.SequenceEqual(File.ReadAllBytes(input)));
                AssertEqual(variant == 1, File.ReadAllBytes(output).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
                var sheet = ParticipantTableReader.Read(output, csvEncoding).Single();
                AssertEqual("001", sheet.Rows[0][0]);
                AssertEqual("ア", sheet.Rows[0][1]);
                AssertEqual("10左", sheet.Rows[0][2]);
                AssertEqual("引用\"符,と\r\n改行", sheet.Rows[0][3]);
                AssertEqual("残す", sheet.Rows[1][1]);
                // An input file can also be selected as the output destination.
                CircleSeatExcelExporter.Export(input, "input", 1, 2, 0, [new("001", "ウ", "3右")], csvEncoding);
                AssertEqual("ウ", ParticipantTableReader.Read(input, csvEncoding).Single().Rows[0][1]);
                var beforeFailure = File.ReadAllBytes(output);
                try
                {
                    CircleSeatExcelExporter.Export(output, "output", 1, 1, 0, [new("001", "ア", "1")], csvEncoding);
                    throw new Exception("Duplicate columns must be rejected.");
                }
                catch (InvalidOperationException) { }
                AssertEqual(true, beforeFailure.SequenceEqual(File.ReadAllBytes(output)));
                if (variant == 2)
                {
                    try
                    {
                        CircleSeatExcelExporter.Export(output, "output", 1, 2, 0, [new("001", "😀", "1")], csvEncoding);
                        throw new Exception("Unencodable text must be rejected.");
                    }
                    catch (System.Text.EncoderFallbackException) { }
                    AssertEqual(true, beforeFailure.SequenceEqual(File.ReadAllBytes(output)));
                }
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CircleSeatValuesAreExported()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"circle-seat-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "target.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("一覧");
                sheet.Cell(1, 1).Value = "サークルID";
                sheet.Cell(1, 2).Value = "ブロック番号";
                sheet.Cell(1, 3).Value = "席番号";
                sheet.Cell(1, 4).Value = "メモ";
                sheet.Cell(2, 1).Value = "1001";
                sheet.Cell(2, 4).Value = "残す";
                sheet.Cell(3, 1).Value = "1002";
                workbook.SaveAs(path);
            }

            var result = CircleSeatExcelExporter.Export(path, "一覧", 1, 2, 0,
            [new CircleSeatExportRow("1001", "ア", "10左"), new CircleSeatExportRow("9999", "イ", "1右")]);
            AssertEqual(1, result.UpdatedRowCount);
            AssertEqual(1, result.MissingCircleCount);
            using var reloaded = new XLWorkbook(path);
            var exportedSheet = reloaded.Worksheet("一覧");
            AssertEqual("ア", exportedSheet.Cell(2, 2).GetString());
            AssertEqual("10左", exportedSheet.Cell(2, 3).GetString());
            AssertEqual("残す", exportedSheet.Cell(2, 4).GetString());
            AssertEqual("", exportedSheet.Cell(3, 2).GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void ChannelRoundTrip()
    {
        var rows = ParticipantTableMapper.Map(new ParticipantTableSheet("Sheet",
            ["ID", "Name", "書籍の有無", "Other", "Other"],
            [new[] { "a", "A", "1", "0.5", "0" }, new[] { "b", "B", "", "1", "0" }]), new(0, 1));
        AssertEqual("1", rows[0].SourceValues["書籍の有無"]);
        AssertEqual("0.5", rows[0].SourceValues["[4] Other"]);
        AssertEqual("0", rows[0].SourceValues["[5] Other"]);
        var project = ProjectJsonSerializer.Load(ReadExample());
        var feature = project.Evaluation.Features[0] with { SourceColumn = "書籍の有無" };
        project = project with
        {
            Participants = project.Participants.Select(item => item with { SourceValues = rows[0].SourceValues }).ToArray(),
            Evaluation = project.Evaluation with { Features = project.Evaluation.Features.Select(item => item.Id == feature.Id ? feature : item).ToArray() },
        };
        var loaded = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(project));
        AssertEqual("書籍の有無", loaded.Evaluation.Features[0].SourceColumn!);
        AssertEqual("1", loaded.Participants[0].SourceValues["書籍の有無"]);
        AssertEqual(ProjectJsonSerializer.Save(project), ProjectJsonSerializer.Save(loaded));
    }

    private static void ChannelNumericPrecision()
    {
        var path = Path.Combine(Path.GetTempPath(), $"channel-precision-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("Values");
                sheet.Cell(1, 1).Value = "ID";
                sheet.Cell(1, 2).Value = "Name";
                sheet.Cell(1, 3).Value = "Weight";
                sheet.Cell(2, 1).Value = 1;
                sheet.Cell(2, 1).Style.NumberFormat.Format = "0000";
                sheet.Cell(2, 2).Value = "Fictional";
                sheet.Cell(2, 3).Value = 0.123456;
                sheet.Cell(2, 3).Style.NumberFormat.Format = "0%";
                workbook.SaveAs(path);
            }
            var rows = ParticipantTableMapper.Map(ParticipantTableReader.Read(path).Single(), new(0, 1));
            AssertEqual("0001", rows[0].CircleId);
            AssertEqual("0.123456", rows[0].SourceValues["Weight"]);
        }
        finally { File.Delete(path); }
    }

    private static string ReadExample()
    {
        var path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "examples",
            "circle-space-project-v1.example.json");
        return File.ReadAllText(path);
    }

    private static void AssertNear(double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 0.000_000_1d)
            throw new InvalidOperationException($"Expected '{expected}', actual '{actual}'.");
    }

    private static void AssertEqual<T>(T expected, T actual)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', actual '{actual}'.");
    }
}
