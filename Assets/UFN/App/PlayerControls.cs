using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UFN.Core;

namespace UFN.App
{
    [Serializable] public sealed class AppPreferences
    {
        public int Version=3;
        public float Master=.8f, Effects=.7f, Music=.5f, Voice=.7f, CameraShake=.6f, Flashes=.5f, Particles=.7f;
        public bool Fullscreen, ShowFrameData=true, Subtitles=true, AutoAccelerate, HorizontalSplit;
        public int Difficulty=1, Quality=1, Resolution, TextSize;
        public bool NoNumpad;
        public PlayerBinding[] Controls={new PlayerBinding(0),new PlayerBinding(1)};
    }
    [Serializable] public sealed class PlayerBinding
    {
        public string[] Keys;
        public string[] Buttons={"dpad/left","dpad/right","dpad/up","dpad/down","buttonWest","buttonNorth","buttonSouth","leftShoulder","buttonEast","rightShoulder","select"};
        public string[] KartKeys;
        public string[] KartButtons={"dpad/left","dpad/right","buttonWest","buttonNorth","buttonSouth","leftShoulder","dpad/down","rightStickPress"};
        public int DeviceMode,PadIndex,HorizontalAxis,VerticalAxis=1;
        public float Deadzone=.32f;
        public bool InvertHorizontal,InvertVertical;
        public PlayerBinding(int p)
        {
            PadIndex=p;
            Keys=p==0?new[]{"A","D","W","S","J","K","L","U","LeftShift","I","O"}:new[]{"LeftArrow","RightArrow","UpArrow","DownArrow","Numpad1","Numpad2","Numpad3","Numpad4","Numpad0","Numpad5","Numpad6"};
            KartKeys=p==0?new[]{"A","D","J","K","L","U","S","F2"}:new[]{"LeftArrow","RightArrow","Numpad1","Numpad2","Numpad3","Numpad4","DownArrow","Backspace"};
        }
    }
    /// <summary>Independent fight/kart maps, paired devices and render-frame edges retained until consumption.</summary>
    public sealed class PlayerControls
    {
        public static readonly string[] Actions={"ESQUERDA","DIREITA","CIMA / PULAR","BAIXO","ATAQUE LEVE","ATAQUE FORTE","CHUTE","DEFESA","ESQUIVA","ULTIMATE","TROCA DE DUPLA"};
        public static readonly string[] KartActions={"ESQUERDA","DIREITA","ACELERAR","FREAR","DRIFT","USAR ITEM","LANÇAR PARA TRÁS","RECUPERAR KART"};
        private readonly InputFrame[] held=new InputFrame[2],pending=new InputFrame[2];
        private readonly KartInput[] kartHeld=new KartInput[2];
        private readonly bool[] itemPending=new bool[2],recoverPending=new bool[2];
        public PlayerBinding[] Bindings={new PlayerBinding(0),new PlayerBinding(1)};
        private bool noNumpad;
        public bool NoNumpad {get=>noNumpad;set{if(value==noNumpad)return;noNumpad=value;ResetKeys();ResetKeys(true);}}
        private int rebindPlayer=-1,rebindAction;
        private bool waitRelease,rebindKart;
        public bool Rebinding=>rebindPlayer>=0;
        public string RebindMessage {get;private set;}="";
        public void Configure(AppPreferences preferences)
        {
            noNumpad=preferences.NoNumpad;
            if(preferences.Controls==null||preferences.Controls.Length!=2)preferences.Controls=new[]{new PlayerBinding(0),new PlayerBinding(1)};
            Bindings=preferences.Controls;
            for(int p=0;p<2;p++)
            {
                var defaults=Defaults(p);var b=Bindings[p]??defaults;Bindings[p]=b;
                b.Keys=ValidMap(b.Keys,defaults.Keys);b.Buttons=ValidMap(b.Buttons,defaults.Buttons);b.KartKeys=ValidMap(b.KartKeys,defaults.KartKeys);b.KartButtons=ValidMap(b.KartButtons,defaults.KartButtons);
                b.DeviceMode=Mathf.Clamp(b.DeviceMode,0,2);b.PadIndex=Mathf.Clamp(b.PadIndex,0,15);b.HorizontalAxis=Mathf.Clamp(b.HorizontalAxis,0,5);b.VerticalAxis=Mathf.Clamp(b.VerticalAxis,0,5);
                b.Deadzone=float.IsNaN(b.Deadzone)||float.IsInfinity(b.Deadzone)? .32f:Mathf.Clamp(b.Deadzone,.1f,.9f);
            }
        }
        private static string[] ValidMap(string[] map,string[] defaults)
        {
            if(map==null||map.Length!=defaults.Length)return defaults;
            for(int i=0;i<map.Length;i++)if(string.IsNullOrEmpty(map[i]))map[i]=defaults[i];return map;
        }
        private PlayerBinding Defaults(int p)
        {
            var b=new PlayerBinding(p);if(!noNumpad)return b;
            if(p==0){b.Keys[4]="F";b.Keys[5]="G";b.Keys[6]="H";b.Keys[7]="R";b.Keys[9]="T";b.Keys[10]="Y";b.KartKeys[2]="F";b.KartKeys[3]="G";b.KartKeys[4]="H";b.KartKeys[5]="R";}
            else{b.Keys[4]="J";b.Keys[5]="K";b.Keys[6]="L";b.Keys[7]="U";b.Keys[8]="RightShift";b.Keys[9]="I";b.Keys[10]="O";b.KartKeys[2]="J";b.KartKeys[3]="K";b.KartKeys[4]="L";b.KartKeys[5]="U";}
            return b;
        }
        public void ResetKeys(bool kart=false){for(int p=0;p<2;p++){var d=Defaults(p);if(kart)Bindings[p].KartKeys=d.KartKeys;else Bindings[p].Keys=d.Keys;}Clear();}
        public void ResetButtons(bool kart=false){for(int p=0;p<2;p++){var d=Defaults(p);if(kart)Bindings[p].KartButtons=d.KartButtons;else Bindings[p].Buttons=d.Buttons;}Clear();}
        public void CyclePad(int p)
        {
            int previous=Bindings[p].PadIndex,next=(previous+1)%Mathf.Max(2,Gamepad.all.Count);Bindings[p].PadIndex=next;
            if(Bindings[1-p].PadIndex==next)Bindings[1-p].PadIndex=previous;Clear();
        }
        public Gamepad Pad(int p)=>Bindings[p].PadIndex<Gamepad.all.Count?Gamepad.all[Bindings[p].PadIndex]:null;
        private KeyControl KeyControl(int p,int a,bool kart=false)
        {
            string code=(kart?Bindings[p].KartKeys:Bindings[p].Keys)[a];
            return Keyboard.current!=null&&Enum.TryParse(code,out Key key)&&key!=Key.None&&Enum.IsDefined(typeof(Key),key)?Keyboard.current[key]:null;
        }
        private ButtonControl PadButton(int p,int a,bool kart=false)=>Pad(p)?.TryGetChildControl<ButtonControl>((kart?Bindings[p].KartButtons:Bindings[p].Buttons)[a]);
        private float Value(int p,int a,bool kart=false)=>Mathf.Max(Bindings[p].DeviceMode!=2&&(KeyControl(p,a,kart)?.isPressed??false)?1:0,Bindings[p].DeviceMode!=1?(PadButton(p,a,kart)?.ReadValue()??0):0);
        private bool Down(int p,int a,bool kart=false)=>Value(p,a,kart)>.5f;
        private bool Pressed(int p,int a,bool kart=false)=>(Bindings[p].DeviceMode!=2&&(KeyControl(p,a,kart)?.wasPressedThisFrame??false))||(Bindings[p].DeviceMode!=1&&(PadButton(p,a,kart)?.wasPressedThisFrame??false));
        public string GetLabel(int p,int action,bool kart=false)=>(kart?Bindings[p].KartKeys:Bindings[p].Keys)[action]+"  /  "+(kart?Bindings[p].KartButtons:Bindings[p].Buttons)[action];
        public void BeginRebind(int p,int action,bool kart=false){rebindPlayer=p;rebindAction=action;rebindKart=kart;waitRelease=true;RebindMessage="";Clear();}
        public bool PollRebind()
        {
            if(!Rebinding)return false;var keyboard=Keyboard.current;var pad=Pad(rebindPlayer);
            if(waitRelease){waitRelease=false;return false;}
            if(keyboard?.escapeKey.wasPressedThisFrame??false){rebindPlayer=-1;return true;}
            if(keyboard!=null)foreach(var k in keyboard.allKeys)if(k.wasPressedThisFrame)
            {
                if(k.keyCode==Key.Enter||k.keyCode==Key.NumpadEnter||k.keyCode==Key.Tab||k.keyCode==Key.F11){RebindMessage="Essa tecla é usada pelos menus. Escolha outra.";return false;}
                RebindKey(k.keyCode.ToString());rebindPlayer=-1;Clear();return true;
            }
            if(pad!=null)foreach(var c in pad.allControls)if(c is ButtonControl b&&!b.synthetic&&b.wasPressedThisFrame)
            {
                string path=c.path.Substring(pad.path.Length+1);if(path=="start"){RebindMessage="Start abre a pausa. Escolha outro botão.";return false;}
                var map=rebindKart?Bindings[rebindPlayer].KartButtons:Bindings[rebindPlayer].Buttons;string previous=map[rebindAction];
                for(int a=0;a<map.Length;a++)if(a!=rebindAction&&map[a]==path)map[a]=previous;
                map[rebindAction]=path;rebindPlayer=-1;Clear();return true;
            }
            return false;
        }
        private void RebindKey(string key)
        {
            var target=rebindKart?Bindings[rebindPlayer].KartKeys:Bindings[rebindPlayer].Keys;string previous=target[rebindAction];
            for(int p=0;p<2;p++)
            {
                var map=rebindKart?Bindings[p].KartKeys:Bindings[p].Keys;
                for(int a=0;a<map.Length;a++)if((p!=rebindPlayer||a!=rebindAction)&&map[a]==key)map[a]=previous;
            }
            target[rebindAction]=key;
        }
        private static float Axis(Gamepad pad,int axis)
        {
            if(pad==null)return 0;
            switch(axis){case 0:return pad.leftStick.x.ReadValue();case 1:return -pad.leftStick.y.ReadValue();case 2:return pad.rightStick.x.ReadValue();case 3:return -pad.rightStick.y.ReadValue();case 4:return pad.leftTrigger.ReadValue();case 5:return pad.rightTrigger.ReadValue();default:return 0;}
        }
        private Vector2 Stick(int p)
        {
            var b=Bindings[p];if(b.DeviceMode==1)return Vector2.zero;var pad=Pad(p);
            return new Vector2(Axis(pad,b.HorizontalAxis)*(b.InvertHorizontal?-1:1),Axis(pad,b.VerticalAxis)*(b.InvertVertical?1:-1));
        }
        public void Sample(bool kart=false)
        {
            if(Rebinding)return;
            for(int p=0;p<2;p++)
            {
                Vector2 stick=Stick(p);float zone=Bindings[p].Deadzone;
                if(kart)
                {
                    float digital=(Down(p,1,true)?1:0)-(Down(p,0,true)?1:0);float analog=Mathf.Sign(stick.x)*Mathf.Clamp01((Mathf.Abs(stick.x)-zone)/(1-zone));
                    kartHeld[p]=new KartInput{Throttle=Value(p,2,true),Brake=Value(p,3,true),Steering=digital!=0?digital:analog,Drift=Down(p,4,true),LaunchBack=Down(p,6,true)||stick.y< -zone};
                    itemPending[p]|=Pressed(p,5,true);recoverPending[p]|=Pressed(p,7,true);continue;
                }
                var f=new InputFrame{Left=Down(p,0)||stick.x< -zone,Right=Down(p,1)||stick.x>zone,Up=Down(p,2)||stick.y>zone,Down=Down(p,3)||stick.y< -zone,Attack=Down(p,4),Heavy=Down(p,5),Kick=Down(p,6),Block=Down(p,7),Dodge=Down(p,8),Power=Down(p,9),Tag=Down(p,10)};
                var edges=pending[p];edges.Up|=Pressed(p,2);edges.Attack|=Pressed(p,4);edges.Heavy|=Pressed(p,5);edges.Kick|=Pressed(p,6);edges.Block|=Pressed(p,7);edges.Dodge|=Pressed(p,8);edges.Power|=Pressed(p,9);edges.Tag|=Pressed(p,10);pending[p]=edges;held[p]=f;
            }
        }
        public InputFrame Consume(int p){var result=InputFrame.FromBits((ushort)(held[p].Bits|pending[p].Bits));pending[p]=default;return result;}
        public KartInput ConsumeKart(int p,bool autoAccelerate){var f=kartHeld[p];if(autoAccelerate)f.Throttle=1;f.UseItem=itemPending[p];itemPending[p]=false;return f;}
        public bool ConsumeKartRecover(int p){bool recover=recoverPending[p];recoverPending[p]=false;return recover;}
        public void Clear(){for(int p=0;p<2;p++){held[p]=pending[p]=default;kartHeld[p]=default;itemPending[p]=recoverPending[p]=false;}}
        public static ushort Pack(InputFrame f)=>f.Bits;
        public static InputFrame Unpack(int bits)=>InputFrame.FromBits((ushort)bits);
    }
    public static class MenuInput
    {
        public static bool Press(Key key)=>Keyboard.current?[key].wasPressedThisFrame??false;
        public static bool Held(Key key)=>Keyboard.current?[key].isPressed??false;
        public static bool Confirm=>Press(Key.Enter)||(Gamepad.current?.buttonSouth.wasPressedThisFrame??false);
        public static bool Back=>Press(Key.Escape)||(Gamepad.current?.buttonEast.wasPressedThisFrame??false);
        public static bool Pause=>Press(Key.Escape)||(Gamepad.current?.startButton.wasPressedThisFrame??false);
        public static int Horizontal=>(Press(Key.RightArrow)||Press(Key.D)||(Gamepad.current?.dpad.right.wasPressedThisFrame??false)?1:0)-(Press(Key.LeftArrow)||Press(Key.A)||(Gamepad.current?.dpad.left.wasPressedThisFrame??false)?1:0);
        public static int Vertical=>(Press(Key.DownArrow)||Press(Key.S)||(Gamepad.current?.dpad.down.wasPressedThisFrame??false)?1:0)-(Press(Key.UpArrow)||Press(Key.W)||(Gamepad.current?.dpad.up.wasPressedThisFrame??false)?1:0);
        public static bool Any=>Keyboard.current?.anyKey.wasPressedThisFrame==true||Mouse.current?.leftButton.wasPressedThisFrame==true||Confirm||Pause||Horizontal!=0||Vertical!=0;
    }
}
