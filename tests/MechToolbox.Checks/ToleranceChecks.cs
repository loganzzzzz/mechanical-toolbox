using System.Globalization;
using MechToolbox.Core;

internal static class ToleranceChecks
{
    public static void Run(BoltModule bolts, Action<bool,string> check, Action<Action,string> reject)
    {
        // Independently transcribed ISO 286 table 1 rows; boundary ownership must never be rounded.
        (decimal max, decimal h12, decimal h13, decimal h14)[] approved =
        [(3,.1m,.14m,.25m),(6,.12m,.18m,.30m),(10,.15m,.22m,.36m),(18,.18m,.27m,.43m),(30,.21m,.33m,.52m),(50,.25m,.39m,.62m)];
        decimal previous=0;
        foreach(var row in approved)
        {
            foreach(var (band,value) in new[]{("H12",row.h12),("H13",row.h13),("H14",row.h14)})
            {
                check(LinearTolerances.Hole(previous+.0001m,band)==new SlotTolerance(0,value),"linear tolerance lower boundary excluded");
                check(LinearTolerances.Hole(row.max,band)==new SlotTolerance(0,value),"linear tolerance upper boundary included");
            }
            previous=row.max;
        }
        reject(()=>LinearTolerances.Hole(0,"H13"),"zero basic size");
        reject(()=>LinearTolerances.Hole(50.001m,"H13"),"unsupported size not extrapolated");
        reject(()=>LinearTolerances.Hole(3,"h13"),"shaft band not treated as hole band");
        var rows=File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"approved-thread-limits.csv")).Skip(1)
            .Select(x=>x.Split(',').Select(v=>decimal.Parse(v,CultureInfo.InvariantCulture)).ToArray()).ToArray();
        check(rows.Length==bolts.Data.Sizes.Count,"all coarse sizes have thread limits");
        foreach(var row in rows)
        {
            var s=bolts.Find(row[0]);var t=MetricThreadLimits.For(s);
            check(t==new ThreadLimits(row[0],row[1],row[2],row[3],row[4],row[5]),"6H source fixture");
            check(t!.Summary.Contains(t.PitchRange) && t.Summary.Contains(t.MinorDeviation),"6H numeric result");
            check(bolts.Summary(s,ClearanceSeries.Medium).Contains(t.MinorRange),"copy thread limits");
            foreach(var c in bolts.Clearances(s))
                check(c.Tolerance.Lower==0 && c.LimitsText.Contains(KeyFormat.Number(c.Diameter+c.Tolerance.Upper)) && bolts.Summary(s,c.Series).Contains(c.DeviationText),"all clearance numeric limits copied");
        }
        check(MetricThreadLimits.For(bolts.Find(8) with { Pitch=1 }) is null,"unsupported thread pitch not silently treated as coarse");
        check(MetricThreadLimits.For(bolts.Find(18))!.PitchMax==16.600m,"M18 6H source correction, not supplier 16.601 typo");
        check(bolts.ExportCsv([bolts.Find(8)]).Contains("\"7.188\",\"7.348\",\"6.647\",\"6.912\""),"CSV exports full precision thread limits");
        var cir=new CirclipModule(new JsonCirclipDataProvider().Load(Path.Combine(AppContext.BaseDirectory,"data","circlips-gb.v1.json")));
        foreach(var s in cir.Data.Sizes)
        {
            var expected=s.GrooveWidth<=3 ? .14m : .18m;
            check(s.WidthTolerance==new SlotTolerance(0,expected) && cir.Summary(s).Contains(s.WidthLimits),"circlip width basic-size group and copy");
        }
        var keys=new KeywayModule(new JsonKeywayDataProvider().Load(Path.Combine(AppContext.BaseDirectory,"data","parallel-keyways-gb.v1.json")));
        foreach(var size in keys.Data.Sizes)
        {
            var r=keys.Query(size.ShaftMax,KeywayFit.Normal);
            check(r.ShaftInspectionLimits.Contains(KeyFormat.Number(r.ShaftInspection-size.DepthUpper)) && r.HubInspectionLimits.Contains(KeyFormat.Number(r.HubInspection+size.DepthUpper)),"keyway opposite inspection deviation directions");
            check(keys.Summary(r).Contains(r.ShaftDepthLimits) && keys.Summary(r).Contains(r.HubDepthLimits),"keyway depth ranges copied");
        }
        var culture=CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");check(cir.Query(CirclipKind.Shaft,30).WidthDeviation.Contains("+0.14 / 0"),"tolerance decimal formatting invariant"); }
        finally { CultureInfo.CurrentCulture=culture; }
    }
}
