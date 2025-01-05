using DokkanDaily.Constants;
using DokkanDaily.Models.Enums;
using Tesseract;

namespace DokkanDaily.Services
{
    public class OcrFormatProvider
    {
        private const string jpnEngineString = "jpn";
        private const string engEngineString = "eng";
        private ParsingMode _parsingMode;

        public void SetParsingMode(ParsingMode parsingMode) => _parsingMode = parsingMode;

        private bool _jp => _parsingMode == ParsingMode.Japanese;

        public string None => _jp ? OcrConstants.NoneJpn : OcrConstants.NoneEng;

        public string TrainDataPath => OcrConstants.TrainDataPath;

        public string BoundingBoxImagePath => _jp ? Path.Combine(TrainDataPath, "boxes_jp.png") : Path.Combine(TrainDataPath, "boxes.png");

        public TesseractEngine CreateTesseractEngine()
        {
            if (_jp)
            {
                var engine = new TesseractEngine(TrainDataPath, jpnEngineString, EngineMode.LstmOnly);
                engine.SetVariable("chop_enable", true);
                engine.SetVariable("use_new_state_cost", false);
                engine.SetVariable("segment_segcost_rating", false);
                engine.SetVariable("enable_new_segsearch", 0);
                engine.SetVariable("language_model_ngram_on", 0);
                engine.SetVariable("textord_force_make_prop_words", false);
                engine.SetVariable("edges_max_children_per_outline", 40);
                engine.SetVariable("preserve_interword_spaces", 1);
                return engine;
            }

            return new TesseractEngine(TrainDataPath, engEngineString, EngineMode.LstmOnly);
        }

        public bool IsValidClearHeader(string clearHeader)
        {
            if (_jp)
            {
                // ocr sucks at kanji :(
                return clearHeader.StartsWith(OcrConstants.StageClearDetailsJpnAlt, StringComparison.InvariantCulture)
                    || clearHeader.StartsWith(OcrConstants.StageClearDetailsJpn, StringComparison.InvariantCulture);
            }

            return string.Equals(clearHeader, OcrConstants.StageClearDetailsEng, StringComparison.InvariantCulture);
        }
    }
}
