using NUnit.Framework;
using UFN.App;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UFN.Tests.Input
{
    // InputTestFixture replaces the runtime with virtual devices and restores it
    // after every test. These checks never consume the user's physical controls.
    public sealed class AppInputTests : InputTestFixture
    {
        private Keyboard keyboard;
        private PlayerControls controls;

        [SetUp]
        public void CreateVirtualControls()
        {
            keyboard=InputSystem.AddDevice<Keyboard>();
            controls=new PlayerControls();controls.Configure(new AppPreferences());
            controls.Bindings[0].DeviceMode=controls.Bindings[1].DeviceMode=1;
        }

        [Test]
        public void ReleasedTapSurvivesRenderFramesWithoutSimulationAndIsConsumedOnce()
        {
            Press(keyboard.jKey);controls.Sample();
            Release(keyboard.jKey);controls.Sample();
            InputSystem.Update();controls.Sample();
            Assert.That(controls.Consume(0).Attack,Is.True,"A short tap must survive frames with zero simulation steps.");
            Assert.That(controls.Consume(0).Attack,Is.False,"A consumed tap must not reappear in an extra simulation step.");
            Press(keyboard.jKey);controls.Sample();
            Assert.That(controls.Consume(0).Attack,Is.True,"A held button is retained independently of its edge.");
            Assert.That(controls.Consume(0).Attack,Is.True);
        }

        [Test]
        public void KartAndFightRemapsRemainIndependentDuringActualInputSampling()
        {
            controls.Bindings[0].Keys[4]="F";controls.Bindings[0].KartKeys[2]="G";
            Press(keyboard.fKey);controls.Sample();
            Assert.That(controls.Consume(0).Attack,Is.True);
            controls.Sample(true);Assert.That(controls.ConsumeKart(0,false).Throttle,Is.Zero);
            Release(keyboard.fKey);Press(keyboard.gKey);controls.Sample(true);
            Assert.That(controls.ConsumeKart(0,false).Throttle,Is.EqualTo(1));
            controls.Sample();Assert.That(controls.Consume(0).Attack,Is.False);
        }

        [Test]
        public void ItemAndRecoveryEdgesSurviveReleaseAndDoNotRepeatWhileHeld()
        {
            controls.Bindings[0].KartKeys[7]="F9";
            InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.U,Key.F9));InputSystem.Update();controls.Sample(true);
            Release(keyboard.uKey);Release(keyboard.f9Key);controls.Sample(true);
            Assert.That(controls.ConsumeKart(0,false).UseItem,Is.True);
            Assert.That(controls.ConsumeKartRecover(0),Is.True);
            Assert.That(controls.ConsumeKart(0,false).UseItem,Is.False);
            Assert.That(controls.ConsumeKartRecover(0),Is.False);
            Press(keyboard.f9Key);controls.Sample(true);Assert.That(controls.ConsumeKartRecover(0),Is.True);
            InputSystem.Update();controls.Sample(true);Assert.That(controls.ConsumeKartRecover(0),Is.False);
        }

        [Test]
        public void RebindingSharedKeyboardSwapsConflictsWithoutChangingKartMap()
        {
            controls.BeginRebind(1,4);Assert.That(controls.PollRebind(),Is.False);
            Press(keyboard.jKey);Assert.That(controls.PollRebind(),Is.True);
            Assert.That(controls.Bindings[1].Keys[4],Is.EqualTo("J"));
            Assert.That(controls.Bindings[0].Keys[4],Is.EqualTo("Numpad1"));
            Assert.That(controls.Bindings[0].KartKeys[2],Is.EqualTo("J"));
            Release(keyboard.jKey);Press(keyboard.numpad1Key);controls.Sample();
            Assert.That(controls.Consume(0).Attack,Is.True);
            Assert.That(controls.Consume(1).Attack,Is.False,"A remap must not silently control both seats.");
        }

        [Test]
        public void ReservedMenuKeyDoesNotCommitARebindAndEscapeCancelsIt()
        {
            controls.BeginRebind(0,4,true);controls.PollRebind();
            Press(keyboard.enterKey);Assert.That(controls.PollRebind(),Is.False);
            Assert.That(controls.Rebinding,Is.True);Assert.That(controls.RebindMessage,Is.Not.Empty);
            Release(keyboard.enterKey);Press(keyboard.escapeKey);Assert.That(controls.PollRebind(),Is.True);
            Assert.That(controls.Rebinding,Is.False);Assert.That(controls.Bindings[0].KartKeys[4],Is.EqualTo("L"));
        }

        [Test]
        public void DeadzoneRejectsNoiseAndAlternateInvertedAxisSteersProgressively()
        {
            var pad=InputSystem.AddDevice<Gamepad>();controls.Bindings[0].DeviceMode=2;
            controls.Bindings[0].PadIndex=0;controls.Bindings[0].Deadzone=.32f;
            Set(pad.leftStick,new Vector2(.2f,0));controls.Sample(true);
            Assert.That(controls.ConsumeKart(0,false).Steering,Is.Zero);
            Set(pad.leftStick,new Vector2(.66f,0));controls.Sample(true);
            Assert.That(controls.ConsumeKart(0,false).Steering,Is.EqualTo(.5f).Within(.02f));
            controls.Bindings[0].HorizontalAxis=2;controls.Bindings[0].InvertHorizontal=true;
            Set(pad.rightStick,new Vector2(-.8f,0));controls.Sample(true);
            Assert.That(controls.ConsumeKart(0,false).Steering,Is.GreaterThan(.6f).And.LessThan(.8f));
        }

        [Test]
        public void GamepadsRemainPairedToSeparateSeats()
        {
            var first=InputSystem.AddDevice<Gamepad>();var second=InputSystem.AddDevice<Gamepad>();
            controls.Bindings[0].DeviceMode=controls.Bindings[1].DeviceMode=2;
            Press(first.buttonWest);controls.Sample();
            Assert.That(controls.Consume(0).Attack,Is.True);Assert.That(controls.Consume(1).Attack,Is.False);
            Release(first.buttonWest);Press(second.buttonWest);controls.Sample();
            Assert.That(controls.Consume(0).Attack,Is.False);Assert.That(controls.Consume(1).Attack,Is.True);
        }

        [Test]
        public void OlderPreferencesKeepCustomFightKeysWhileMissingKartMapsAreRepaired()
        {
            var prefs=new AppPreferences();prefs.Controls[0].Keys[4]="F";
            prefs.Controls[0].KartKeys=null;prefs.Controls[0].KartButtons=null;prefs.Controls[0].Deadzone=float.NaN;
            controls.Configure(prefs);
            Assert.That(controls.Bindings[0].Keys[4],Is.EqualTo("F"));
            Assert.That(controls.Bindings[0].KartKeys.Length,Is.EqualTo(8));
            Assert.That(controls.Bindings[0].KartButtons.Length,Is.EqualTo(8));
            Assert.That(controls.Bindings[0].Deadzone,Is.EqualTo(.32f));
            Press(keyboard.jKey);controls.Sample(true);Assert.That(controls.ConsumeKart(0,false).Throttle,Is.EqualTo(1));
        }
    }
}
