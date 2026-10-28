using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class FrontierAudio : MonoBehaviour
    {
        public bool MusicPlaying=>_music!=null&&_music.isPlaying;
        public int PlayedEffects {get;private set;}
        public int ExpansionVariationCount=>6;
        public int ExpansionVariantsUsed {get;private set;}
        public int IncomingAttackAlerts {get;private set;}
        public int IncomingInvasionAlerts {get;private set;}
        private float _nextAttackAlert,_nextInvasionAlert;
        private AudioSource _music,_ambience,_ui,_world,_intro,_naval;
        private int _attackSequence,_invasionSequence,_attackUsed,_invasionUsed;
        public int AttackVariantsUsed=>Bits(_attackUsed);public int InvasionVariantsUsed=>Bits(_invasionUsed);
        private static int Bits(int n)=>((n&1)!=0?1:0)+((n&2)!=0?1:0)+((n&4)!=0?1:0);
        private FrontierSettings _settings;
        private readonly Dictionary<string,AudioClip> _clips=new Dictionary<string,AudioClip>();
        private float _duckUntil,_musicEnvelope=.10f,_introEnvelope=1;
        private bool _cinematic=true,_cinematicStarted;
        public void Initialize(FrontierSettings settings)
        {
            _settings=settings;
            _music=gameObject.AddComponent<AudioSource>();_ambience=gameObject.AddComponent<AudioSource>();
            _ui=gameObject.AddComponent<AudioSource>();_world=gameObject.AddComponent<AudioSource>();_intro=gameObject.AddComponent<AudioSource>();_naval=gameObject.AddComponent<AudioSource>();
            foreach(string name in new[]{"score","ambience","intro","click","hover","order","denied","expand","battle","promotion","sail","landfall"})
                _clips[name]=Resources.Load<AudioClip>("Audio/"+name);
            for(int i=0;i<6;i++)_clips["expand_"+i]=Resources.Load<AudioClip>("Audio/expand_"+i);
            for(int i=0;i<3;i++)_clips["click_"+i]=Resources.Load<AudioClip>("Audio/click_"+i);
            for(int i=0;i<3;i++)foreach(string threat in new[]{"incoming_attack","incoming_invasion"})_clips[threat+"_"+i]=Resources.Load<AudioClip>("Audio/"+threat+"_"+i);
            _clips["incoming_attack"]=_clips["incoming_attack_0"]??_clips["battle"];_clips["incoming_invasion"]=_clips["incoming_invasion_0"]??_clips["sail"];
            foreach(var source in new[]{_music,_ambience,_ui,_world,_intro,_naval}){source.playOnAwake=false;source.spatialBlend=0;}
            _music.clip=_clips["score"];_music.loop=true;_music.Play();
            _ambience.clip=_clips["ambience"];_ambience.loop=true;_ambience.Play();
            _intro.clip=_clips["intro"];ApplyVolumes();
        }
        public void StartCinematic()
        {
            if(_cinematicStarted)return;_cinematicStarted=true;_cinematic=true;_introEnvelope=1;_intro.Play();
        }
        public void EndCinematic()=>_cinematic=false;
        public void ResetThreatCooldowns(){_nextAttackAlert=_nextInvasionAlert=0;_world?.Stop();_naval?.Stop();}
        private void Update()
        {
            float dt=Time.unscaledDeltaTime;
            float target=_cinematic ? .08f : (Time.unscaledTime<_duckUntil ? .40f : .64f);
            _musicEnvelope=Mathf.Lerp(_musicEnvelope,target,1-Mathf.Exp(-2.5f*dt));
            if(!_cinematic)_introEnvelope=Mathf.Lerp(_introEnvelope,0,1-Mathf.Exp(-7*dt));
            ApplyVolumes();
        }
        public void ApplyVolumes()
        {
            if(_music==null)return;
            _music.volume=_settings.Master*_settings.Music*_musicEnvelope;
            _ambience.volume=_settings.Master*_settings.Music*.16f;
            _ui.volume=_settings.Master*_settings.Effects*.78f;
            _world.volume=_settings.Master*_settings.Effects*.74f;
            _naval.volume=_settings.Master*_settings.Effects*.74f;
            _intro.volume=_settings.Master*(_settings.Music*.85f+_settings.Effects*.15f)*_introEnvelope;
        }
        public bool HasClip(string name)=>_clips.TryGetValue(name,out var clip)&&clip!=null;
        public void Play(string name,float gain=1)
        {
            if(name!="incoming_attack"&&name!="incoming_invasion")return;
            if(_ui==null)return;
            bool invasion=name=="incoming_invasion";int index=(invasion?_invasionSequence++:_attackSequence++)%3;
            if(!_clips.TryGetValue(name+"_"+index,out var clip)||clip==null)_clips.TryGetValue(name,out clip);if(clip==null)return;
            var source=invasion?_naval:_world;source.pitch=index==0?1:index==1?.985f:1.015f;
            float overlap=(invasion?_world:_naval).isPlaying?.72f:1;source.PlayOneShot(clip,Mathf.Clamp01(gain)*overlap);PlayedEffects++;
            if(invasion)_invasionUsed|=1<<index;else _attackUsed|=1<<index;
            _duckUntil=Time.unscaledTime+clip.length+.15f;
        }
        public void Expansion()
        { }
        public void IncomingAttack()
        {if(Time.unscaledTime<_nextAttackAlert)return;_nextAttackAlert=Time.unscaledTime+8;IncomingAttackAlerts++;Play("incoming_attack",.72f);}
        public void IncomingInvasion()
        {if(Time.unscaledTime<_nextInvasionAlert)return;_nextInvasionAlert=Time.unscaledTime+10;IncomingInvasionAlerts++;Play("incoming_invasion",.76f);}
        public void Battle()
        { }
        private void OnApplicationQuit(){foreach(var source in new[]{_music,_ambience,_ui,_world,_intro,_naval})if(source!=null)source.Stop();}
    }
}
