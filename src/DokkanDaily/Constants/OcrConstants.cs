namespace DokkanDaily.Constants
{
    public static class OcrConstants
    {
        public static string TrainDataPath => @"./wwwroot/tessdata";
        public static string StageClearDetailsEng => "Stage Clear"; // technically Stage Clear Details but people are sick in the head and watch videos PIP while playing dokkan
        public static string StageClearDetailsEngAlt => "Clear Details";
        public static string StageClearDetailsJpn => "クリアタイム"; // womp womp, ocr cant tell the difference between り and リ
        public static string StageClearDetailsJpnAlt => "クりアタイム"; // it also cant read 詳細
        public static string NoneEng => "None";
        public static string NoneJpn => "なし";
        public static string DbcTag => "DBC*";
    }
}
