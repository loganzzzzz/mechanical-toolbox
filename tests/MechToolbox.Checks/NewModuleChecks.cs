using System.Globalization;
using System.Text.Json.Nodes;
using MechToolbox.Core;

internal static class NewModuleChecks
{
    public static void Run(BoltModule bolts, Action<bool, string> check, Action<Action, string> reject)
    {
        var provider = new JsonKeywayDataProvider();
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "parallel-keyways-gb.v1.json"));
        var data = provider.Parse(json);
        var keys = new KeywayModule(data);
        var approved = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "approved-keyways.csv")).Skip(1)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(line => line.Split(',').Select(x => decimal.Parse(x, CultureInfo.InvariantCulture)).ToArray()).ToArray();
        check(data.Sizes.Count == approved.Length, "complete keyway dataset");
        foreach (var a in approved)
        {
            var s = keys.Query(a[1], KeywayFit.Normal).Size;
            decimal[] actual = [s.ShaftMin, s.ShaftMax, s.Width, s.Height, s.ShaftDepth, s.HubDepth, s.DepthUpper,
                s.RadiusMin, s.RadiusMax, s.N9.Lower, s.N9.Upper, s.Js9.Upper, s.P9.Lower, s.P9.Upper, s.H9.Upper, s.D10.Lower, s.D10.Upper];
            check(actual.SequenceEqual(a), $"keyway original-table values {s.Section}");
            foreach (var fit in KeywayModule.Fits)
            {
                var r = keys.Query((a[0] + a[1]) / 2, fit.Fit);
                check(r.Size.Width == a[2] && r.ShaftInspection == r.ShaftDiameter - a[4] && r.HubInspection == r.ShaftDiameter + a[5], "keyway dimension chain");
                var tol = fit.Fit switch { KeywayFit.Normal => (s.N9, s.Js9), KeywayFit.Tight => (s.P9, s.P9), _ => (s.H9, s.D10) };
                check(r.ShaftWidthTolerance == tol.Item1 && r.HubWidthTolerance == tol.Item2, "shaft/hub fit selection");
            }
            check(keys.Query(a[0] + .001m, KeywayFit.Normal).Size == s, "keyway lower boundary just above");
            if (a[0] > 6) check(keys.Query(a[0], KeywayFit.Normal).Size.Width < s.Width, "exact boundary belongs to preceding row");
        }
        check(keys.Query(6, KeywayFit.Normal).Size.Width == 2 && keys.Query(500, KeywayFit.Normal).Size.Width == 100, "keyway end boundaries inclusive");
        check(keys.Query(12, KeywayFit.Normal).Size.RadiusMax == .16m, "b4 radius follows original standard");
        check(keys.Query(500, KeywayFit.Normal).Size.HubDepth == 19.5m, "b100 hub depth follows original standard over handbook typo");
        var r50 = keys.Query(50, KeywayFit.Normal);
        check(r50.ShaftInspection == 44.5m && r50.HubInspection == 53.8m, "50mm inspection dimensions");
        check(r50.ShaftInspectionDeviation.Contains("0 / -0.2") && r50.HubInspectionDeviation.Contains("+0.2 / 0"), "shaft inspection tolerance reverses sign");
        check(keys.Query(8, KeywayFit.Normal).HubWidthTolerance.Text == "+0.0125 / -0.0125", "JS9 four decimals retained");
        check(keys.Summary(r50).Contains("轴径选键属于设计推荐") && keys.Summary(r50).Contains("GB/T 1095-2003"), "keyway copy includes source and selection scope");
        foreach (decimal d in new[] { 0m, 5.999m, 500.001m }) reject(() => keys.Query(d, KeywayFit.Normal), "unsupported keyway diameter");
        reject(() => keys.Query(30, (KeywayFit)99), "unknown fit");
        void Bad(Action<JsonObject> mutate, string name)
        {
            var node = JsonNode.Parse(json)!.AsObject(); mutate(node);
            reject(() => provider.Parse(node.ToJsonString()), name);
        }
        Bad(n => n["schemaVersion"] = 2, "future keyway schema");
        Bad(n => n["sizes"] = null, "null keyway sizes");
        Bad(n => n["sizes"]![0] = null, "null keyway row");
        Bad(n => n["sizes"]![1]!["shaftMin"] = 7, "overlapping shaft ranges");
        Bad(n => n["sizes"]![1]!["shaftMin"] = 9, "gap in shaft ranges");
        Bad(n => n["sizes"]![0]!["shaftDepth"] = 0, "zero keyway depth");
        Bad(n => n["sizes"]![0]!["js9"] = null, "null tolerance");
        Bad(n => n["sizes"]![0]!["js9"]!["lower"] = .01, "non symmetric JS9");
        Bad(n => n["sizes"]![0]!["h9"]!["lower"] = .01, "H9 lower must be zero");
        Bad(n => n["sizeSourceId"] = "missing", "missing keyway source");
        Bad(n => n["extra"] = 1, "unknown keyway field");
        var vm = new KeywayViewModel(keys);
        foreach (var input in new[] { "", "wrong", "0", "501", "30,5", "30.0001", "1e2" })
        {
            vm.DiameterText = "50"; vm.DiameterText = input;
            check(!vm.HasResult && vm.Result is null && vm.Error.Length > 0, "invalid keyway input hides stale results");
        }
        vm.SelectedFit = KeywayModule.Fits[2];
        check(vm.Result is null, "fit change while invalid cannot resurrect stale result");
        vm.DiameterText = "50.125";
        check(vm.Result is { ShaftDiameter: 50.125m, Fit.Fit: KeywayFit.Loose }, "decimal shaft input with retained fit");

        // Every supported forward diameter must be discoverable by the corresponding reverse query.
        foreach (var s in bolts.Data.Sizes)
        {
            (BoltHoleKind Kind, decimal D)[] holes = [(BoltHoleKind.Fine, s.Fine), (BoltHoleKind.Medium, s.Medium), (BoltHoleKind.Coarse, s.Coarse), (BoltHoleKind.TapDrill, s.TapDrill)];
            foreach (var h in holes)
            {
                check(BoltReverse.Find(bolts, h.D).Any(x => x.Size == s && x.Kind == h.Kind), "all four hole uses included in unified reverse search");
                check(BoltReverse.Find(bolts, h.D + 3).Any(x => x.Size == s && x.Kind == h.Kind && x.Difference == -3), "lower hole boundary included");
                check(!BoltReverse.Find(bolts, h.D + 3.001m).Any(x => x.Size == s && x.Kind == h.Kind), "lower hole boundary outside excluded");
                if (h.D > 3)
                    check(BoltReverse.Find(bolts, h.D - 3).Any(x => x.Size == s && x.Kind == h.Kind && x.Difference == 3), "upper hole boundary included");
                if (h.D > 3.001m)
                    check(!BoltReverse.Find(bolts, h.D - 3.001m).Any(x => x.Size == s && x.Kind == h.Kind), "upper hole boundary outside excluded");
            }
        }
        var ambiguous = BoltReverse.Find(bolts, 2.2m);
        check(ambiguous.Where(x => x.Difference == 0).Select(x => x.Size.Spec).SequenceEqual(new[] { "M1.8", "M2" }), "all exact ambiguous candidates kept before nearby holes");
        check(BoltReverse.Find(bolts, 6.8m).First() is { Size.Diameter: 8, Kind: BoltHoleKind.TapDrill, Difference: 0 }, "exact drill result labelled and ranked first without selector");
        var mixed = BoltReverse.Find(bolts, 12);
        check(mixed.Count == 12 && mixed[0] is { Size.Diameter: 10, Kind: BoltHoleKind.Coarse, Difference: 0 } &&
            mixed[1] is { Size.Diameter: 14, Kind: BoltHoleKind.TapDrill, Difference: 0 }, "12mm includes all 12 nearby clearance and tap drill candidates");
        check(mixed.Select(x => x.NominalDiameter).SequenceEqual(new decimal[] { 12, 12, 11, 13, 10.5m, 13.5m, 10.2m, 10, 14, 14.5m, 9, 15 }), "nearby holes sorted by absolute difference then designation");
        check(mixed.All(x => x.Spec == x.Size.Spec && !x.Spec.Contains('×')), "reverse candidates display only coarse designation");
        check(mixed.All(x => x.NominalDiameter >= 9 && x.NominalDiameter <= 15 && x.Difference == x.NominalDiameter - 12), "12mm range and signed differences");
        check(mixed.Any(x => x.DifferenceText == "-3") && mixed.Any(x => x.DifferenceText == "+3") && mixed[0].DifferenceText == "0", "difference output distinguishes smaller larger and exact holes");
        check(BoltReverse.Find(bolts, 8.45m).First() is { Size.Diameter: 8, Kind: BoltHoleKind.Fine, Difference: -0.05m }, "decimal input finds nearest stored diameter without extrapolation");
        check(BoltReverse.Find(bolts, 11.111m).First() is { NominalDiameter: 11, Difference: -0.111m }, "three decimal input preserves signed difference");
        check(BoltReverse.Find(bolts, .001m).All(x => x.NominalDiameter > 0), "small positive input only returns actual positive holes");
        check(BoltReverse.Find(bolts, 1000).Count == 0, "far outside data yields no candidates");
        reject(() => BoltReverse.Find(bolts, 0), "zero reverse input");
        reject(() => BoltReverse.Find(bolts, -1), "negative reverse input");
        reject(() => BoltReverse.Find(bolts, 1001), "oversized reverse input");
        var reverse = new BoltReverseViewModel(bolts) { DiameterText = "2.2" };
        check(reverse.Matches.Count > 2 && reverse.Matches.Take(2).All(x => x.Difference == 0), "reverse view model includes nearby and exact candidates");
        foreach (var input in new[] { "", "oops", "0", "1001", "8,4", "8.4001" })
        {
            reverse.DiameterText = "8.4"; reverse.DiameterText = input;
            check(reverse.Matches.Count == 0 && reverse.Message.Length > 0, "invalid reverse input clears candidates");
        }
        reverse.DiameterText = "12";
        check(reverse.Matches.Count == 12 && reverse.Message.Contains("9～15 mm"), "view model shows inclusive search range");
        reverse.DiameterText = "8.4";
        check(reverse.Matches.First() is { Size.Diameter: 8, Difference: 0 }, "reverse recovers from invalid input");
        reverse.DiameterText = "1000";
        check(reverse.Matches.Count == 0 && reverse.Message.Contains("997～1003 mm"), "no-result message includes range and clears stale candidates");
        var registry = new ModuleRegistry(); registry.Register(bolts); registry.Register(keys);
        check(registry.Modules.OfType<IStandardDataModule>().Count() == 2, "both modules expose sources through shared interface");
    }
}
