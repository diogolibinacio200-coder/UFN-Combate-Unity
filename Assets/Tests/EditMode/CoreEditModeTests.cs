using System;
using System.IO;
using NUnit.Framework;
using UFN.Core;
using UnityEngine;

namespace UFN.Tests.EditMode
{
    public sealed class CoreEditModeTests
    {
        private GameCatalog catalog;

        [OneTimeSetUp]
        public void LoadShippedCatalog()
        {
            catalog = JsonUtility.FromJson<GameCatalog>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Content/catalog.json")));
            Assert.That(catalog, Is.Not.Null);
        }

        [Test]
        public void ShippedCatalogContainsOriginalIdentitiesAndChainLinks()
        {
            Assert.That(catalog.Characters.Length, Is.EqualTo(16));
            Assert.That(catalog.Normals.Length, Is.EqualTo(9));
            Assert.That(catalog.Chains.Length, Is.EqualTo(2));
            Assert.That(catalog.Character(3).Key, Is.EqualTo("diogo"));
            Assert.That(catalog.Character(14).Name, Does.Contain("ANDRÉ"));
            Assert.That(catalog.Character(15).Name, Is.EqualTo("KELVIN"));
            for (int id = 0; id < 16; id++)
            {
                Assert.That(catalog.Character(id).Specials.Length, Is.EqualTo(3));
                Assert.That(catalog.Character(id).Ultimate.Family, Is.EqualTo("super"));
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void BossDifficultySurvivesCheckpointAndRoundReset(int difficulty)
        {
            float[] andre = { 1100, 1150, 1250, 1300 }, kelvin = { 1150, 1250, 1300, 1400 };
            var world = new CombatWorld(catalog, 14, 15, 71, difficulty);
            var snapshot = world.CaptureSnapshot();
            world.Difficulty = 0; world.ResetRound(); world.RestoreSnapshot(snapshot); world.ResetRound();
            Assert.That(world.Fighters[0].Hp, Is.EqualTo(andre[difficulty]));
            Assert.That(world.Fighters[1].Hp, Is.EqualTo(kelvin[difficulty]));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        [TestCase(12)] [TestCase(13)] [TestCase(15)]
        public void EveryCapturingUltimateDealsItsAuthoredDamageAndReleasesActors(int id)
        {
            var world = new CombatWorld(catalog, id, id == 0 ? 3 : 0);
            var attacker = world.Fighters[0]; var defender = world.Fighters[1];
            attacker.X = 90; defender.X = 90 + (id == 0 ? 85 : id == 3 || id == 8 || id == 12 ? 265 : 140);
            attacker.Meter = 100; world.Step(new InputFrame { Power = true }, default);
            int hits = 0, finalHits = 0;
            for (int frame = 0; frame < 450; frame++)
            {
                world.Step(default, default);
                foreach (var e in world.Events) if (e.Type == "super_hit") { hits++; if (e.Final) finalHits++; }
            }
            Assert.That(hits, Is.EqualTo(id == 1 || id == 9 ? 15 : 6));
            Assert.That(finalHits, Is.EqualTo(1));
            Assert.That(defender.Hp, Is.EqualTo(id == 4 ? 800 : 720).Within(.001f));
            Assert.That(world.Cinematic.Active, Is.False);
            Assert.That(CombatWorld.IsFree(attacker) && CombatWorld.IsFree(defender), Is.True);
            Assert.That(attacker.UltimateTint || defender.UltimateTint, Is.False);
        }

        [Test]
        public void TeamCheckpointPreservesIdentityResourcesAndDeterministicContinuation()
        {
            var world = new CombatWorld(catalog, 3, 4, 19); world.SetupTeams(0, 1);
            world.Fighters[0].Hp = 610; world.Fighters[0].Meter = 71; world.Fighters[0].Skin = 1;
            world.Step(new InputFrame { Tag = true }, default);
            Assert.That(world.Reserve(0).Hp, Is.EqualTo(610));
            Assert.That(world.Reserve(0).Skin, Is.EqualTo(1));
            var snapshot = world.CaptureSnapshot(); ulong hash = world.StateHash();
            for (int i = 0; i < 301; i++) world.Step(default, default);
            Assert.That(world.TryTag(0), Is.True); ulong future = world.StateHash();
            world.RestoreSnapshot(snapshot);
            Assert.That(world.StateHash(), Is.EqualTo(hash));
            Assert.That(world.Fighters[0], Is.SameAs(world.Teams[0][world.TeamActive[0]]));
            for (int i = 0; i < 301; i++) world.Step(default, default);
            Assert.That(world.TryTag(0), Is.True);
            Assert.That(world.StateHash(), Is.EqualTo(future));
        }

        [Test]
        public void InvalidCheckpointAndTeamSelectionLeaveLiveMatchUntouched()
        {
            var world = new CombatWorld(catalog, 3, 4); world.SetupTeams(0, 1); ulong before = world.StateHash();
            var snapshot = world.CaptureSnapshot(); snapshot.Teams[0][0].Hp = float.NaN;
            Assert.Throws<ArgumentException>(() => world.RestoreSnapshot(snapshot));
            Assert.That(world.StateHash(), Is.EqualTo(before));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.ConfigureTeams(4, 99, 5, 6));
            Assert.That(world.StateHash(), Is.EqualTo(before));
        }

        [TestCase(3)] [TestCase(5)] [TestCase(8)] [TestCase(10)]
        public void UnityJsonRetainsClassicTowerRouteAndDiogoSkin(int count)
        {
            var run = ClassicRun.Create(true, 3, 1, count, 2, 17); run.AdvanceVictory();
            var restored = ClassicRun.Restore(JsonUtility.FromJson<ClassicSnapshot>(JsonUtility.ToJson(run.ExportSnapshot())));
            CollectionAssert.AreEqual(run.Order, restored.Order);
            Assert.That(restored.Skin, Is.EqualTo(1));
            Assert.That(restored.Character, Is.EqualTo(3));
            Assert.That(restored.Index, Is.EqualTo(1));
            Assert.That(restored.Order[count], Is.EqualTo(14));
            Assert.That(restored.Order[count + 1], Is.EqualTo(15));
        }

        [Test]
        public void UnityJsonRetainsMirrorTournamentSlotsAndMixedControllers()
        {
            var bracket = LocalBracket.Create(new[] { 3, 3, 4, 6 }, new[] { 0, 1, 0, 0 }, new[] { false, false, true, true });
            bracket.RecordWinner(1);
            var restored = LocalBracket.Restore(JsonUtility.FromJson<LocalBracketSnapshot>(JsonUtility.ToJson(bracket.ExportSnapshot())));
            Assert.That(restored.PairA, Is.EqualTo(2)); Assert.That(restored.PairB, Is.EqualTo(3));
            restored.RecordWinner(0);
            Assert.That(restored.PairA, Is.EqualTo(1)); Assert.That(restored.PairB, Is.EqualTo(2));
            Assert.That(restored.Skin(1), Is.EqualTo(1));
            Assert.That(restored.IsCpu(1), Is.False); Assert.That(restored.IsCpu(2), Is.True);
            restored.RecordWinner(0); Assert.That(restored.Champion, Is.EqualTo(1));
        }

        [Test]
        public void UnityJsonRetainsSurvivalHealthScoreAndNextRival()
        {
            var run = SurvivalRun.Create(3, 1, 0); run.AdvanceVictory(700.9f, 61);
            var restored = SurvivalRun.Restore(JsonUtility.FromJson<SurvivalSnapshot>(JsonUtility.ToJson(run.ExportSnapshot())));
            Assert.That(restored.Health, Is.EqualTo(880.9f).Within(.001f));
            Assert.That(restored.Score, Is.EqualTo(1720)); Assert.That(restored.Opponent, Is.EqualTo(4));
            Assert.That(restored.Skin, Is.EqualTo(1));
        }

        private sealed class UnityProfileCodec : IProfileCodec
        {
            public string Encode(ProfileData value) => JsonUtility.ToJson(value, true);
            public ProfileData Decode(string json) => JsonUtility.FromJson<ProfileData>(json);
        }

        [Test]
        public void UnityJsonSavesFreshProfileWithoutInventingAnActiveCampaign()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "ufn-profile-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "profile.json");
            try
            {
                var store = new ProfileStore(path, new UnityProfileCodec());
                var profile = store.Load(); profile.TotalWins = 7; profile.SelectedCharacter = 3; profile.SelectedSkin = 1;
                Assert.DoesNotThrow(() => store.Save(profile));
                var loaded = store.Load();
                Assert.That(loaded.Campaign, Is.Null);
                Assert.That(loaded.TotalWins, Is.EqualTo(7));
                Assert.That(loaded.SelectedSkin, Is.EqualTo(1));
                loaded.TotalWins = 8; store.Save(loaded);
                Assert.That(store.Load().TotalWins, Is.EqualTo(8));
                Assert.That(File.Exists(path + ".bak"), Is.True);
                File.WriteAllText(path, "{broken");
                Assert.That(store.Load().TotalWins, Is.EqualTo(7));
                Assert.That(store.RecoveryMessage, Is.Not.Empty);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                    Directory.Delete(directory);
                }
            }
        }

        [Test]
        public void UnityJsonProfileRetainsRealCampaignAndPendingBenefit()
        {
            var run = CampaignRun.Create(CampaignMode.Tower, 3, 91);
            run.RecordVictory();
            var profile = new ProfileData { TotalWins = 1, Campaign = run.Export() };
            var codec = new UnityProfileCodec(); var loaded = codec.Decode(codec.Encode(profile));
            loaded.ValidateAndMigrate();
            var restored = CampaignRun.Restore(loaded.Campaign);
            Assert.That(restored.Stage, Is.EqualTo(1));
            Assert.That(restored.CharacterId, Is.EqualTo(3));
            Assert.That(restored.OpponentId, Is.EqualTo(run.OpponentId));
            CollectionAssert.AreEqual(run.BenefitChoices, restored.BenefitChoices);
            Assert.That(restored.AwaitingBenefit, Is.True);
        }

        [Test]
        public void ProfileRejectsDamagedNonemptyCampaignInsteadOfDiscardingIt()
        {
            var snapshot = CampaignRun.Create(CampaignMode.Tower, 3, 91).Export();
            snapshot.RandomState = 0;
            var profile = new ProfileData { Campaign = snapshot };
            Assert.Throws<ArgumentException>(() => profile.ValidateAndMigrate());
            Assert.That(profile.Campaign, Is.SameAs(snapshot));
        }
    }
}
