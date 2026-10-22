using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UFN.Core;

namespace UFN.App
{
    public sealed class GameAudio : MonoBehaviour
    {
        private readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        private readonly AudioSource[] voices=new AudioSource[12];
        private AudioSource music;
        private AudioSource narrator;
        private int voice;
        private float nextCommentary;
        public event Action<string,float> Subtitle;
        public float EffectsVolume=.8f, MusicVolume=.45f, VoiceVolume=.7f;
        public void Initialize(string folder)
        {
            music=gameObject.AddComponent<AudioSource>(); music.loop=true; music.playOnAwake=false;
            narrator=gameObject.AddComponent<AudioSource>();narrator.playOnAwake=false;
            for(int i=0;i<voices.Length;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;}
            foreach(string path in Directory.GetFiles(folder,"*.wav"))
            {
                try { clips[Path.GetFileNameWithoutExtension(path)]=LoadWave(path); }
                catch(Exception ex){Debug.LogWarning("Audio skipped: "+Path.GetFileName(path)+" "+ex.Message);}
            }
        }
        private static AudioClip LoadWave(string path)
        {
            using(var reader=new BinaryReader(File.OpenRead(path)))
            {
                if(new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("Not RIFF");
                reader.ReadInt32(); if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("Not WAVE");
                ushort format=0, channels=0, bits=0;int sampleRate=0;byte[] data=null;
                while(reader.BaseStream.Position+8<=reader.BaseStream.Length)
                {
                    string id=new string(reader.ReadChars(4));int size=reader.ReadInt32();long next=reader.BaseStream.Position+size+(size&1);
                    if(size<0||next>reader.BaseStream.Length+1)throw new InvalidDataException("Invalid chunk");
                    if(id=="fmt "){format=reader.ReadUInt16();channels=reader.ReadUInt16();sampleRate=reader.ReadInt32();reader.ReadInt32();reader.ReadUInt16();bits=reader.ReadUInt16();}
                    else if(id=="data") data=reader.ReadBytes(size);
                    reader.BaseStream.Position=Math.Min(next,reader.BaseStream.Length);
                }
                if(data==null||channels==0||sampleRate==0||format!=1||bits!=16)throw new InvalidDataException("Expected PCM16");
                float[] samples=new float[data.Length/2];for(int i=0;i<samples.Length;i++)samples[i]=(short)(data[i*2]|(data[i*2+1]<<8))/32768f;
                var clip=AudioClip.Create(Path.GetFileNameWithoutExtension(path),samples.Length/channels,channels,sampleRate,false);clip.SetData(samples,0);return clip;
            }
        }
        public void Play(string key,float pitch=1)
        {
            if(!clips.TryGetValue(key,out var clip))return;
            bool speech=key.StartsWith("voice_")||key.StartsWith("narrator_");
            var source=speech?narrator:voices[voice++%voices.Length];source.Stop();source.clip=clip;source.pitch=pitch;source.volume=speech?VoiceVolume:EffectsVolume;source.Play();
            if(speech){string caption=Caption(key);if(caption!=null)Subtitle?.Invoke(caption,clip.length/Mathf.Max(.1f,Mathf.Abs(pitch)));nextCommentary=Time.unscaledTime+clip.length+.8f;}
        }
        private static string Caption(string key)
        {
            switch(key)
            {
                case "voice_round_one":return "Primeiro round!";
                case "voice_round_two":return "Segundo round!";
                case "voice_final_round":return "Round final!";
                case "voice_fight":return "Lutem!";
                case "voice_player_one_wins":return "Jogador um vence!";
                case "voice_player_two_wins":return "Jogador dois vence!";
                case "voice_counter":return "Contra-ataque!";
                case "voice_guard_break_voice":return "Guarda quebrada!";
                case "voice_perfect":return "Perfeito!";
                case "voice_ko":return "Nocaute!";
                case "voice_final_hit":return "Golpe final!";
                default:return null;
            }
        }
        public void Music(int stage=-1)
        {
            string key=stage<0?"music_menu":stage==8?"music_victory":"music_arena_"+(stage%8).ToString("00");
            if(!clips.TryGetValue(key,out var clip)){foreach(var pair in clips)if(pair.Key.StartsWith("music_")){clip=pair.Value;break;}}
            if(clip==null)return;if(music.clip==clip&&music.isPlaying)return;music.clip=clip;music.volume=MusicVolume;music.Play();
        }
        public void Consume(IList<CombatEvent> events)
        {
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];
                switch(e.Type)
                {
                    case "hit": case "super_hit": Play(e.Final||e.Damage>65?"hit_heavy":"hit",1+(e.Combo%4)*.035f);break;
                    case "block":case "perfect_block":Play("block");break;
                    case "parry":case "counter":Play("counter_hit");if(Time.unscaledTime>=nextCommentary)Play("voice_counter");break;
                    case "guard_break":if(Time.unscaledTime>=nextCommentary)Play("voice_guard_break_voice");break;
                    case "ko":Play("ko_impact");Play("voice_ko");break;
                    case "jump":Play("jump");break;
                    case "super":Play("super");break;
                }
            }
        }
        void Update(){if(music!=null)music.volume=MusicVolume;if(narrator!=null)narrator.volume=VoiceVolume;}
        void OnDestroy(){foreach(var c in clips.Values)if(c!=null)Destroy(c);}
    }
}
