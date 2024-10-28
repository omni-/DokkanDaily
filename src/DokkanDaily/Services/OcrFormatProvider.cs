using DokkanDaily.Constants;
using DokkanDaily.Models.Enums;
using Tesseract;

namespace DokkanDaily.Services
{
    public class OcrFormatProvider
    {
        private ParsingMode _parsingMode { get; set; }

        public void SetParsingMode(ParsingMode parsingMode) => _parsingMode = parsingMode;

        private Dictionary<string, string> EngDict { get; init; }

        private Dictionary<string, string> JpDict { get; init; }

        private bool _jp => _parsingMode == ParsingMode.Japanese;

        public string ParsingModeEngineString => _jp ? jpnEngineString : engEngineString;

        public string Nickname => _jp ? OcrConstants.NicknameJpn : OcrConstants.NicknameEng;

        public string ClearTime => _jp ? OcrConstants.ClearTimeJpn : OcrConstants.ClearTimeEng;

        public string ItemsUsed => _jp ? OcrConstants.ItemsUsedJpn : OcrConstants.ItemsUsedEng;

        public string PersonalBest => _jp ? OcrConstants.PersonalBestJpn : OcrConstants.PersonalBestEng;

        public string None => _jp ? OcrConstants.NoneJpn : OcrConstants.NoneEng;

        public string Clear => _jp ? OcrConstants.ClearJpn : OcrConstants.ClearEng;

        public string ClearAlt => OcrConstants.ClearJpnAlt;

        public string ClearTimeAlt => OcrConstants.ClearTimeJpnAlt;

        public string TrainDataPath => OcrConstants.TrainDataPath;

        public TesseractEngine TesseractEngine => _jp ? new TesseractEngine(TrainDataPath, jpnEngineString, EngineMode.LstmOnly) : new TesseractEngine(TrainDataPath, engEngineString, EngineMode.LstmOnly);

        private static string jpnEngineString => "jpn";

        private static string engEngineString => "eng";

        public string GetText(Page p) => _jp ? p.GetText().Replace(" ", "") : p.GetText();
    }
}
