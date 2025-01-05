using DokkanDaily.Constants;
using DokkanDaily.Models.Enums;
using Tesseract;

namespace DokkanDaily.Services
{
    public class OcrFormatProvider
    {
        private ParsingMode _parsingMode { get; set; }

        public void SetParsingMode(ParsingMode parsingMode) => _parsingMode = parsingMode;

        private bool _jp => _parsingMode == ParsingMode.Japanese;

        public string None => _jp ? OcrConstants.NoneJpn : OcrConstants.NoneEng;

        public string TrainDataPath => OcrConstants.TrainDataPath;

        public string BoundingBoxImagePath => _jp ? Path.Combine(TrainDataPath, "boxes_jp.png") : Path.Combine(TrainDataPath, "boxes.png");

        public TesseractEngine TesseractEngine => _jp ? new TesseractEngine(TrainDataPath, jpnEngineString, EngineMode.LstmOnly) : new TesseractEngine(TrainDataPath, engEngineString, EngineMode.LstmOnly);

        private static string jpnEngineString => "jpn";

        private static string engEngineString => "eng";

        public string GetText(Page p) => _jp ? p.GetText().Replace(" ", "") : p.GetText();

        public void SetEngineOptions(TesseractEngine engine)
        {
            if (_jp)
            {
                engine.SetVariable("chop_enable", true);
                engine.SetVariable("use_new_state_cost", false);
                engine.SetVariable("segment_segcost_rating", false);
                engine.SetVariable("enable_new_segsearch", 0);
                engine.SetVariable("language_model_ngram_on", 0);
                engine.SetVariable("textord_force_make_prop_words", false);
                engine.SetVariable("edges_max_children_per_outline", 40);
                engine.SetVariable("preserve_interword_spaces", 1);
            }
        }

        public bool EnsureValidClearHeader(string clearHeader)
        {
            if (_jp)
            {
                // ocr sucks at kanji :(
                return clearHeader.Contains(OcrConstants.StageClearDetailsJpnAlt, StringComparison.InvariantCulture) 
                    || clearHeader.Contains(OcrConstants.StageClearDetailsJpn, StringComparison.InvariantCulture);
            }

            return string.Equals(clearHeader, OcrConstants.StageClearDetailsEng, StringComparison.InvariantCulture);
        }
    }
}
