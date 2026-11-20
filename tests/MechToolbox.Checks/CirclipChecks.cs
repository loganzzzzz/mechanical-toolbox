using System.Globalization;
using System.Text.Json.Nodes;
using MechToolbox.Core;

internal static class CirclipChecks
{
    public static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        var json=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"data","circlips-gb.v1.json"));
        var provider=new JsonCirclipDataProvider(); var data=provider.Parse(json); var module=new CirclipModule(data);
        var fixture=File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"approved-circlips.csv")).Skip(1).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Split(',')).ToArray();
        check(data.Sizes.Count==fixture.Length && module.Sizes(CirclipKind.Shaft).Count==86 && module.Sizes(CirclipKind.Bore).Count==88,"circlip full A-type source range");
        foreach(var row in fixture)
        {
            var kind=row[0]=="shaft"?CirclipKind.Shaft:CirclipKind.Bore;
            var v=row.Skip(1).Select(x=>decimal.Parse(x,CultureInfo.InvariantCulture)).ToArray();
            var s=module.Query(kind,v[0]);
            check(s.Thickness==v[1] && s.FreeDiameter==v[2] && s.GrooveDiameter==v[3] && s.GrooveWidth==v[4] && s.NominalDepth==v[5] && s.EdgeMin==v[6] && s.GrooveTolerance.Lower==v[7] && s.GrooveTolerance.Upper==v[8] && s.SourcePage==(int)v[9],$"source circlip {kind} {v[0]}");
            check(s.NeedsReview || Math.Abs(s.Diameter-s.GrooveDiameter)/2==s.NominalDepth,"circlip nominal radial depth");
            check(module.Summary(s).Contains(s.Designation) && module.Summary(s).Contains("H13"),"circlip copy designation and groove band");
        }
        check(module.Query(CirclipKind.Shaft,30) is {Thickness:1.5m,GrooveDiameter:28.6m,GrooveWidth:1.6m,EdgeMin:2.1m},"shaft30 critical values");
        check(module.Query(CirclipKind.Bore,30) is {Thickness:1.2m,GrooveDiameter:31.4m,GrooveWidth:1.3m,EdgeMin:2.1m},"bore30 different thickness and groove direction");
        check(module.Query(CirclipKind.Shaft,4).GrooveTolerance.Lower==-.05m,"shaft4 original table deviation");
        check(module.Query(CirclipKind.Bore,30).GrooveTolerance.Upper==.25m,"bore30 original table deviation");
        var disputed=module.Query(CirclipKind.Shaft,40);
        check(disputed.NeedsReview && disputed.GrooveText=="⌀37" && disputed.DepthText=="1.25","shaft40 displays current GB table values without manufacturer substitution");
        check(disputed.GrooveDeviation=="上 / 下偏差：0 / -0.25 mm" && disputed.GrooveLimits=="槽径极限：36.75 ～ 37 mm","shaft40 groove numeric deviation and limits restored");
        check(disputed.WidthText=="1.85 H13" && disputed.WidthLimits=="槽宽极限：1.85 ～ 1.99 mm" && disputed.EdgeText=="≥ 3.8","shaft40 other dimensions remain available");
        check(disputed.ReviewNote.Contains("国标原表") && disputed.ReviewNote.Contains("1.5") && module.Summary(disputed).Contains(disputed.ReviewNote),"shaft40 dimensional inconsistency remains explicit in copied results");
        check(module.Summary(disputed).Contains("槽径 d₂：⌀37") && module.Summary(disputed).Contains("表列名义槽深 t：1.25"),"shaft40 copied query includes both GB table dimensions");
        check(!module.Query(CirclipKind.Bore,40).NeedsReview,"bore40 unaffected by shaft40 conflict");
        reject(()=>module.Query(CirclipKind.Shaft,27),"no extrapolated shaft27 circlip");
        reject(()=>module.Query(CirclipKind.Bore,29),"no interpolated bore29 circlip");
        reject(()=>module.Query((CirclipKind)99,30),"invalid circlip kind");
        var vm=new CirclipViewModel(module);
        foreach(var kind in CirclipViewModel.Kinds)
        {
            vm.SelectedKind=kind;
            foreach(var d in vm.Diameters) { vm.DiameterText=KeyFormat.Number(d);check(vm.Result==module.Query(kind.Kind,d)&&vm.Error=="","VM all circlip nominal diameters"); }
        }
        foreach(var text in new[]{"","invalid","-30","0","30.0001","1e2","1,000","30.1","301"})
        { vm.DiameterText=text;check(vm.Result is null && !vm.HasResult && vm.Error.Length>0,"invalid or unlisted circlip diameter clears result"); }
        vm.SelectedKind=CirclipViewModel.Kinds[0];vm.DiameterText="3";check(vm.Result?.Diameter==3,"shaft minimum");
        vm.SelectedKind=CirclipViewModel.Kinds[1];check(vm.Result is null,"shaft-to-bore switch clears unsupported diameter");
        vm.DiameterText="30";check(vm.Result?.GrooveDiameter==31.4m,"circlip recovers after invalid input");
        vm.SelectedKind=CirclipViewModel.Kinds[0];vm.DiameterText="40";check(vm.Result?.NeedsReview==true && vm.Result.GrooveText=="⌀37","shaft40 query restores dimensions and note");
        vm.DiameterText="42";check(vm.Result?.NeedsReview==false && vm.Result.ReviewNote=="","switching away clears shaft40 review note");
        vm.SelectedKind=CirclipViewModel.Kinds[1];vm.DiameterText="40";check(vm.Result?.NeedsReview==false && vm.Result.GrooveDiameter==42.5m,"bore40 remains independent from shaft40");
        vm.DiameterText="30";
        var oldCulture=CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");
        check(vm.Result!.GrooveText=="⌀31.4" && module.Summary(vm.Result).Contains("1.3 H13"),"circlip decimal format invariant");
        CultureInfo.CurrentCulture=oldCulture;
        foreach(var mutation in new Action<JsonObject>[] {
            n=>n["schemaVersion"]=2,n=>n["updatedOn"]="invalid",n=>n["sources"]=null,n=>n["sources"]![0]!["url"]="file:///x",
            n=>n["sizes"]=null,n=>n["sizes"]=new JsonArray(),n=>n["sizes"]![0]=null,
            n=>n["sizes"]!.AsArray().Add(n["sizes"]![0]!.DeepClone()),n=>n["sizes"]![0]!["kind"]="invalid",
            n=>n["sizes"]![0]!["grooveWidth"]=.1,n=>n["sizes"]![0]!["grooveDiameter"]=10,
            n=>n["sizes"]![0]!["grooveTolerance"]!["upper"]=.1,n=>n["sizes"]![0]!["nominalDepth"]=10,
            n=>n["sizes"]![0]!["reviewNote"]=null,n=>n["unknown"]=1 })
        { var n=JsonNode.Parse(json)!.AsObject();mutation(n);reject(()=>provider.Parse(n.ToJsonString()),"corrupt circlip dataset rejected"); }
    }
}
