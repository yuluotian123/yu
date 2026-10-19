using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Goap;

namespace GameLogic.Validation;

public static class GoapValidation
{
    private static int _passed;
    public static int Main()
    {
        try
        {
            Test("unknown is not false", () =>
            {
                var state = new GoapWorldState();
                Check(!GoapCondition.Is("open", GoapValue.Bool(false)).IsMet(state));
                Check(!new GoapCondition("open", GoapComparison.NotEqual, GoapValue.Bool(true)).IsMet(state));
            });
            Test("immutable effects, location replacement, numeric bounds", () =>
            {
                var state = State(food: 1);
                string before = state.StateKey;
                var next = state.With(GoapEffect.Add("food", -1), GoapEffect.Set("location", GoapValue.Id("home")));
                Check(state.StateKey == before && state["food"].Integer == 1 && next["food"].Integer == 0);
                Check(next["location"].Symbol == "home" && state["location"].Symbol == "street");
                Check(!next.TryApply(new[] { GoapEffect.Add("food", -1) }, out _));
                Check(!state.TryApply(new[] { GoapEffect.Add("missing", 1) }, out _));
            });
            Test("eat carried food", () => Check(Find(State(food: 1)).Plan.Steps.Single().Operation == "eat"));
            Test("buy is cheaper than travelling home", () =>
                Check(Find(State()).Plan.Steps.Select(action => action.Operation).SequenceEqual(new[] { "move", "buy", "eat" })));
            Test("closed shop selects home", () =>
                Check(Find(State(shopOpen: false)).Plan.Steps.Select(action => action.Operation).SequenceEqual(new[] { "move", "take", "eat" })));
            Test("no coins selects home", () => Check(Find(State(coins: 0)).Plan.Steps[0].TargetId == "home"));
            Test("no legal food source is NoPlan", () => Check(Find(State(homeStock: 0, shopStock: 0)).Status == GoapSearchStatus.NoPlan));
            Test("already satisfied returns empty plan", () =>
            {
                var state = State().With(GoapEffect.Set("hungry", GoapValue.Bool(false)));
                var result = Find(state);
                Check(result.Status == GoapSearchStatus.AlreadySatisfied && result.Plan.Steps.Count == 0);
            });
            Test("one-node slices equal unbounded search", () =>
            {
                var sliced = Find(State(), slice: 1);
                var full = Find(State());
                Check(sliced.Plan.Steps.Select(action => action.Id).SequenceEqual(full.Plan.Steps.Select(action => action.Id)));
                Check(sliced.Plan.Cost == full.Plan.Cost && sliced.Expanded == full.Expanded);
            });
            Test("slice budget returns Pending", () =>
            {
                var search = new GoapPlanner(State(), HungerGoal(), Actions());
                Check(search.Advance(1) == GoapSearchStatus.Pending);
                Check(search.Expanded == 1);
            });
            Test("node and depth limits do not report NoPlan", () =>
            {
                Check(Find(State(), limits: new(1, 16)).Status == GoapSearchStatus.LimitReached);
                Check(Find(State(), limits: new(100, 1)).Status == GoapSearchStatus.LimitReached);
            });
            Test("shallower costly path survives cheaper deep path", () =>
            {
                GoapAction Edge(string from, string to, double cost) => new(from + to, "move", to, cost,
                    new[] { GoapCondition.Is("place", GoapValue.Id(from)) }, new[] { GoapEffect.Set("place", GoapValue.Id(to)) });
                var state = new GoapWorldState().With(GoapEffect.Set("place", GoapValue.Id("S")));
                var search = new GoapPlanner(state, new("end", GoapCondition.Is("place", GoapValue.Id("G"))),
                    new[] { Edge("S", "A", 0), Edge("A", "T", 0), Edge("S", "T", 2), Edge("T", "G", 1) }, limits: new(100, 2));
                Finish(search);
                Check(search.Status == GoapSearchStatus.Found && search.Plan.Cost == 3);
            });
            Test("zero-cost no-op and cycles terminate", () =>
            {
                var actions = Actions().Concat(new[] { new GoapAction("noop", "noop", "", 0,
                    Array.Empty<GoapCondition>(), Array.Empty<GoapEffect>()) });
                var search = new GoapPlanner(State(homeStock: 0, shopStock: 0), HungerGoal(), actions);
                Finish(search);
                Check(search.Status == GoapSearchStatus.NoPlan && search.Expanded < 30);
            });
            Test("action ordering is stable", () =>
            {
                var search = new GoapPlanner(State(), HungerGoal(), Actions().Reverse());
                Finish(search);
                Check(search.Plan.Steps.Select(action => action.Id).SequenceEqual(Find(State()).Plan.Steps.Select(action => action.Id)));
            });
            Test("invalidate discards a pending or complete result", () =>
            {
                var search = Find(State());
                search.Invalidate();
                Check(search.Plan == null && search.Advance() == GoapSearchStatus.Invalidated);
            });
            Test("dynamic action costs follow the simulated state",()=>
            {
                var state=new GoapWorldState().With(GoapEffect.Set("arrived",GoapValue.Bool(false)));
                var move=new GoapAction("move","place",0,s=>s.With(GoapEffect.Set("arrived",GoapValue.Bool(true))),_=>2);
                var work=new GoapAction("work","place",0,s=>s["arrived"].Integer==1?s.With(GoapEffect.Set("done",GoapValue.Bool(true))):null,s=>s["arrived"].Integer==1?1:100);
                var direct=new GoapAction("direct","",0,s=>s.With(GoapEffect.Set("done",GoapValue.Bool(true))),_=>5);
                var search=new GoapPlanner(state,new GoapGoal("done",s=>s["done"].Integer==1),new[]{direct,work,move});Finish(search);
                Check(search.Plan.Cost==3&&search.Plan.Steps.Select(a=>a.Id).SequenceEqual(new[]{"move","work"}));
            });
            Test("invalid dynamic costs are not executable",()=>
            {
                var goal=new GoapGoal("done",s=>s["done"].Integer==1);
                var actions=new[]{double.NaN,double.PositiveInfinity,-1d}.Select((cost,i)=>new GoapAction("bad"+i,"",0,s=>s.With(GoapEffect.Set("done",GoapValue.Bool(true))),_=>cost));
                var search=new GoapPlanner(new(),goal,actions);Finish(search);Check(search.Status==GoapSearchStatus.NoPlan);
            });
            Console.WriteLine($"NPC_GOAP_VALIDATION_PASS {_passed} scenarios");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Test(string name, Action action) { action(); _passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool condition) { if (!condition) throw new InvalidOperationException("Validation assertion failed."); }
    private static GoapGoal HungerGoal() => new("hunger", GoapCondition.Is("hungry", GoapValue.Bool(false)));
    private static GoapWorldState State(int food = 0, int coins = 5, int homeStock = 2, int shopStock = 1, bool shopOpen = true)
        => new GoapWorldState().With(GoapEffect.Set("hungry", GoapValue.Bool(true)), GoapEffect.Set("food", GoapValue.Number(food)),
            GoapEffect.Set("coins", GoapValue.Number(coins)), GoapEffect.Set("homeStock", GoapValue.Number(homeStock)),
            GoapEffect.Set("shopStock", GoapValue.Number(shopStock)), GoapEffect.Set("open", GoapValue.Bool(shopOpen)),
            GoapEffect.Set("location", GoapValue.Id("street")));
    private static GoapAction[] Actions() => new[]
    {
        new GoapAction("eat", "eat", "", .5, new[] { GoapCondition.Min("food", 1) },
            new[] { GoapEffect.Add("food", -1), GoapEffect.Set("hungry", GoapValue.Bool(false)) }),
        new GoapAction("move.home", "move", "home", 4, Array.Empty<GoapCondition>(), new[] { GoapEffect.Set("location", GoapValue.Id("home")) }),
        new GoapAction("move.shop", "move", "shop", 1, Array.Empty<GoapCondition>(), new[] { GoapEffect.Set("location", GoapValue.Id("shop")) }),
        new GoapAction("take.home", "take", "home", 1,
            new[] { GoapCondition.Is("location", GoapValue.Id("home")), GoapCondition.Min("homeStock", 1) },
            new[] { GoapEffect.Add("homeStock", -1), GoapEffect.Add("food", 1, maximum: 4) }),
        new GoapAction("buy.shop", "buy", "shop", 1,
            new[] { GoapCondition.Is("location", GoapValue.Id("shop")), GoapCondition.Is("open", GoapValue.Bool(true)),
                GoapCondition.Min("shopStock", 1), GoapCondition.Min("coins", 2) },
            new[] { GoapEffect.Add("shopStock", -1), GoapEffect.Add("coins", -2), GoapEffect.Add("food", 1, maximum: 4) })
    };
    private static GoapPlanner Find(GoapWorldState state, int slice = 10000, GoapSearchLimits limits = null)
    {
        var search = new GoapPlanner(state, HungerGoal(), Actions(), limits: limits);
        Finish(search, slice);
        return search;
    }
    private static void Finish(GoapPlanner search, int slice = 10000)
    {
        for (int i = 0; i < 10000 && search.Status == GoapSearchStatus.Pending; i++) search.Advance(slice);
        Check(search.Status != GoapSearchStatus.Pending);
    }
}
