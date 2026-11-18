using System;
using System.Collections.Generic;

namespace Evolution.Volume
{
    // Deterministic display name of a dynasty founder, hashed from the biological
    // identity string with the same FNV constants inheritance already uses. The same
    // world on the same seed always shows the same name; no random numbers are drawn
    // and nothing in the model is written.
    public static class LineageName
    {
        public const int Catalog = 32;
        static readonly string[] Words = {
            "Корень","Иней","Искра","Волна","Нектар","Роса","Струя","Пена",
            "Тина","Тень","Луч","Мгла","Ил","Вьюн","Зерно","Клубок",
            "Лепесток","Пузырь","Узор","Хвоинка","Чешуйка","Ядро","Жгут","Сфера",
            "Оболочка","Плод","Нитка","Течение","Крап","Ресница","Фитиль","Стебель"
        };
        public static string For(string identity)
        {
            if (string.IsNullOrEmpty(identity)) return null;
            uint hash = 2166136261;
            foreach (char ch in identity) hash = unchecked((hash ^ ch) * 16777619);
            return Words[hash % Catalog];
        }
    }
    // The family tree of one played dynasty, built only from verified observations:
    // the controlled founder, division and death records of the LifeJournal, and
    // streamed agents whose biological genealogy matches a tracked parent. A member
    // missing from the loaded area is "не наблюдается", never silently dead; a stale
    // live snapshot cannot resurrect a confirmed death. Read-only for the simulation.
    public sealed class LineageTree
    {
        public struct Node
        {
            public string Identity, Name, ParentIdentity;
            public int Generation, CellId, LineageId;
            public long BirthSequence;
            public bool Dead, AliveNow, IsFounder;
            public int Kind;
        }
        public const int MemberLimit = 64;
        readonly Dictionary<string, Node> members = new Dictionary<string, Node>(StringComparer.Ordinal);
        readonly List<Node> ordered = new List<Node>();
        VolumeWorld latest;
        string founderIdentity;
        long journalSequence;
        double startedAt = double.NaN;
        bool dirty = true;
        public int Count => members.Count;
        public bool HasFounder => founderIdentity != null;
        public string FounderIdentity => founderIdentity;
        public string FounderName => LineageName.For(founderIdentity);
        public int FounderGeneration { get; private set; } = int.MinValue;
        public bool IsDynasty(string identity) => identity != null && members.ContainsKey(identity);
        public bool IsDead(string identity) => members.TryGetValue(identity, out var node) && node.Dead;
        public static string StatusText(in Node node) => node.AliveNow ? "жив" : node.Dead ? "погиб" : "не наблюдается";
        void Capture(VolumeAgent agent, string parent, long birthSequence)
        {
            if (members.Count >= MemberLimit) return;
            members[agent.Identity] = new Node {
                Identity = agent.Identity, Name = LineageName.For(agent.Identity), ParentIdentity = parent,
                Generation = agent.Biology.Generation, CellId = agent.Biology.Id,
                LineageId = agent.Biology.LineageId, BirthSequence = birthSequence,
                IsFounder = parent == null
            };
            dirty = true;
        }
        bool Matches(Node node, VolumeAgent agent)
            => node.CellId == agent.Biology.Id && node.LineageId == agent.Biology.LineageId
                && node.Generation == agent.Biology.Generation;
        // Call once per simulation step with the same controlled index the player uses.
        public void Record(VolumeWorld world, int controlled)
        {
            if (world == null) return;
            // A rebuilt streamed world has its own journal; never replay the old one.
            if (!ReferenceEquals(world, latest)) { latest = world; journalSequence = 0; }
            if (controlled >= 0 && controlled < world.Count) {
                var a = world.Get(controlled);
                if (a.Alive && !string.IsNullOrEmpty(a.Identity) && !members.ContainsKey(a.Identity)) {
                    Capture(a, null, 0);
                    if (founderIdentity == null) {
                        founderIdentity = a.Identity;
                        FounderGeneration = a.Biology.Generation;
                        startedAt = world.Time;
                    }
                }
            }
            for (long s = journalSequence + 1; world.Life.TryRead(s, out LifeEvent e); s++) {
                journalSequence = s;
                if (e.Kind == LifeEventKind.Division) {
                    var parent = e.Agent.Identity;
                    if (parent != null && members.TryGetValue(parent, out var parentNode)
                        && e.Child.Identity != null && !members.ContainsKey(e.Child.Identity))
                        Capture(e.Child, parent, e.Sequence);
                } else if (e.Kind == LifeEventKind.Death && e.Agent.Identity != null
                    && members.TryGetValue(e.Agent.Identity, out var deadNode)) {
                    deadNode.Dead = true;
                    members[e.Agent.Identity] = deadNode;
                }
            }
            // Present-day observations: alive members, their niche, and confirmed deaths.
            foreach (var key in new List<string>(members.Keys)) {
                var node = members[key];
                node.AliveNow = false;
                members[key] = node;
            }
            for (int i = 0; i < world.Count; i++) {
                var a = world.Get(i);
                if (a.Identity == null || !members.TryGetValue(a.Identity, out var node)) continue;
                // A reused identity string with incompatible biology is not our member.
                if (!Matches(node, a)) continue;
                if (a.Alive) {
                    node.AliveNow = true;
                    node.Kind = LineageChallenge.NicheKind(world.EnvironmentAt(a.Position));
                } else node.Dead = true;
                members[a.Identity] = node;
            }
            // Streamed descendants whose division was missed by the journal window.
            if (members.Count < MemberLimit && !double.IsNaN(startedAt))
                for (int i = 0; i < world.Count; i++) {
                    var a = world.Get(i);
                    if (string.IsNullOrEmpty(a.Identity) || members.ContainsKey(a.Identity) || !a.DivisionChild) continue;
                    var parentIdentity = CellEvolution.ParentIdentity(a);
                    if (parentIdentity == null || !members.TryGetValue(parentIdentity, out var parentNode)) continue;
                    if (parentNode.CellId != a.Biology.ParentId || parentNode.LineageId != a.Biology.LineageId
                        || parentNode.Generation != a.Biology.Generation - 1) continue;
                    Capture(a, parentIdentity, long.MaxValue - (long)(a.DivisionAt * 1000));
                }
        }
        // Breadth-first from the founder; children in journal birth order.
        public List<Node> Ordered()
        {
            if (!dirty) return ordered;
            ordered.Clear();
            if (founderIdentity != null && members.TryGetValue(founderIdentity, out var founder)) {
                var children = new Dictionary<string, List<Node>>(StringComparer.Ordinal);
                foreach (var pair in members) {
                    if (pair.Value.ParentIdentity == null) continue;
                    if (!children.TryGetValue(pair.Value.ParentIdentity, out var list))
                        children[pair.Value.ParentIdentity] = list = new List<Node>();
                    list.Add(pair.Value);
                }
                foreach (var list in children.Values) list.Sort((a, b) => a.BirthSequence != b.BirthSequence
                    ? a.BirthSequence.CompareTo(b.BirthSequence) : string.CompareOrdinal(a.Identity, b.Identity));
                var queue = new Queue<Node>();
                queue.Enqueue(founder);
                while (queue.Count > 0) {
                    var node = queue.Dequeue();
                    ordered.Add(node);
                    if (children.TryGetValue(node.Identity, out var kids))
                        foreach (var kid in kids) queue.Enqueue(kid);
                }
            }
            dirty = false;
            return ordered;
        }
    }
}
