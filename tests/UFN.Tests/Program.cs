using System.Text.Json;
using UFN.Core;
using UFN.Tests;

string root=args.Length>0?Path.GetFullPath(args[0]):Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
var options=new JsonSerializerOptions { IncludeFields=true, PropertyNameCaseInsensitive=true };
var catalog=JsonSerializer.Deserialize<GameCatalog>(File.ReadAllText(Path.Combine(root,"Assets/StreamingAssets/Content/catalog.json")),options);
var failures=new List<string>();
string suite=args.Length>1?args[1]:"all";
if(suite!="all" && suite!="combat" && suite!="modes" && suite!="progression")throw new ArgumentException("Unknown suite: "+suite);
if(suite=="all" || suite=="combat")failures.AddRange(CombatTests.Run(catalog));
if(suite=="all" || suite=="modes")failures.AddRange(ModesTests.Run());
if(suite=="all" || suite=="progression")failures.AddRange(ProgressionTests.Run());
foreach(var failure in failures)Console.WriteLine("FAIL: "+failure);
Console.WriteLine($"CORE VALIDATION ({suite}): {CombatTests.Checks+ModesTests.Checks+ProgressionTests.Checks} checks, {failures.Count} failures");
Environment.ExitCode=failures.Count==0?0:1;
