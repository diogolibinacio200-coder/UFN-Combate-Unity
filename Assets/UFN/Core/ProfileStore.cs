using System;
using System.IO;
using System.Text;

namespace UFN.Core
{
    [Serializable]
    public sealed class ProfileData
    {
        public const int CurrentVersion = 2;
        public int Version = CurrentVersion;
        public int TotalWins;
        public int TotalLosses;
        public int TowerClears;
        public int TournamentWins;
        public int BestSurvival;
        public int BestKartMilliseconds;
        public int SelectedCharacter;
        public int SelectedSkin;
        public float MusicVolume = .7f;
        public float EffectsVolume = .8f;
        public CampaignSnapshot Campaign;

        public void RecordCombatResult(CampaignRun run, bool victory)
        {
            if (victory) TotalWins++; else TotalLosses++;
            if (run == null) return;
            Campaign = run.Export();
            if (run.Mode == CampaignMode.Survival) BestSurvival = Math.Max(BestSurvival, run.Wins);
            if (run.Complete && run.Won && victory)
            {
                if (run.Mode == CampaignMode.Tower) TowerClears++;
                if (run.Mode == CampaignMode.Tournament) TournamentWins++;
            }
        }
        public void RecordKartResult(KartRace race)
        {
            if (race == null || !race.Racers[0].Finished) return;
            int value = (int)Math.Round(race.Racers[0].FinishTick * 1000.0 / 60.0);
            if (BestKartMilliseconds == 0 || value < BestKartMilliseconds) BestKartMilliseconds = value;
        }
        public void ValidateAndMigrate()
        {
            if (Version < 1 || Version > CurrentVersion) throw new InvalidDataException("Versão do perfil não suportada.");
            if (TotalWins < 0 || TotalLosses < 0 || TowerClears < 0 || TournamentWins < 0 || BestSurvival < 0 || BestKartMilliseconds < 0 || SelectedCharacter < 0 || SelectedCharacter > 13 || SelectedSkin < 0 || SelectedSkin > 1 || float.IsNaN(MusicVolume) || float.IsInfinity(MusicVolume) || float.IsNaN(EffectsVolume) || float.IsInfinity(EffectsVolume)) throw new InvalidDataException("Dados do perfil inválidos.");
            MusicVolume = Math.Max(0, Math.Min(1, MusicVolume));
            EffectsVolume = Math.Max(0, Math.Min(1, EffectsVolume));
            // Unity serializes inline reference fields by value: an absent campaign
            // can return as a zeroed snapshot. Only that exact empty representation
            // means "no campaign"; a populated but invalid run must still fail.
            if (Campaign != null) Campaign = IsEmptyCampaign(Campaign) ? null : CampaignRun.Restore(Campaign).Export();
            Version = CurrentVersion;
        }

        private static bool IsEmptyCampaign(CampaignSnapshot value)
        {
            if ((value.Version != 0 && value.Version != 1) || value.Mode != CampaignMode.Tower ||
                value.CharacterId != 0 || value.Seed != 0 || value.RandomState != 0 ||
                value.Stage != 0 || value.Wins != 0 || value.Losses != 0 || value.Complete || value.Won ||
                (value.Opponents != null && value.Opponents.Length != 0) ||
                (value.Bracket != null && value.Bracket.Length != 0) ||
                (value.Choices != null && value.Choices.Length != 0)) return false;
            if (value.Benefits == null) return true;
            if (value.Benefits.Length != 0 && value.Benefits.Length != 5) return false;
            foreach (int benefit in value.Benefits) if (benefit != 0) return false;
            return true;
        }
    }

    public interface IProfileCodec
    {
        string Encode(ProfileData value);
        ProfileData Decode(string json);
    }

    /// <summary>JSON encoding belongs to the host. Durable atomic replacement belongs here.</summary>
    public sealed class ProfileStore
    {
        public string Path { get; }
        public string RecoveryMessage { get; private set; } = "";
        private readonly IProfileCodec codec;
        public ProfileStore(string path, IProfileCodec codec)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Caminho do perfil vazio.");
            Path = System.IO.Path.GetFullPath(path);
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }
        public ProfileData Load()
        {
            RecoveryMessage = "";
            if (TryRead(Path, out var profile)) return profile;
            if (TryRead(Path + ".bak", out profile))
            {
                RecoveryMessage = "Perfil recuperado da cópia de segurança.";
                return profile;
            }
            if (File.Exists(Path) || File.Exists(Path + ".bak")) RecoveryMessage = "Os arquivos de perfil não puderam ser lidos. Um perfil novo foi iniciado; os arquivos anteriores serão preservados até salvar.";
            return new ProfileData();
        }
        private bool TryRead(string path, out ProfileData value)
        {
            value = null;
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return false;
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal) || json.IndexOf("\"Version\"", StringComparison.Ordinal) < 0) return false;
                value = codec.Decode(json);
                if (value == null) return false;
                value.ValidateAndMigrate();
                return true;
            }
            // The codec may throw its own parser exception type (Unity or another JSON library).
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            { value = null; return false; }
        }
        public void Save(ProfileData profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.ValidateAndMigrate();
            string json = codec.Encode(profile);
            // Validate the actual serialized result before touching either durable copy.
            ProfileData verification = codec.Decode(json);
            if (verification == null) throw new InvalidDataException("O serializador produziu um perfil vazio.");
            verification.ValidateAndMigrate();
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            if (bytes.Length > 1024 * 1024) throw new InvalidDataException("Perfil maior que o limite de 1 MB.");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string temporary = Path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(Path))
                {
                    bool mainValid = TryRead(Path, out _);
                    // An invalid main file must never replace a usable backup.
                    File.Replace(temporary, Path, mainValid ? Path + ".bak" : null);
                }
                else { File.Move(temporary, Path); File.Copy(Path, Path + ".bak", true); }
                RecoveryMessage = "";
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
