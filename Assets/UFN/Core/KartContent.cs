using System;

namespace UFN.Core
{
    public static class KartContent
    {
        public static readonly string[] TrackNames={"CIRCUITO DO CAMPUS","VOLTA DA VILA BELGA","CENTRO DE SANTA MARIA","DESAFIO UNIVERSITÁRIO","UFN / ANDRADAS"};
        public static readonly string[] Names={"ARTHUR","VITOR DO BEM","MARIA","DIOGO","MIRKOS","FELIPE","MURILO","GABRIEL","FABIANO","VITOR DO MAL","LORENZO","LEONARDO","CRISTIAN","FERNANDO"};
        public static readonly string[] Keys={"arthur","vitorbem","maria","diogo","mirkos","felipe","murilo","gabriel","fabiano","vitormal","lorenzo","leonardo","cristian","fernando"};
        public static readonly string[] Signatures={"HALTER","GRITO","CAPIVARA","RAÍZES","DRONE","BOLA COM CURVA","HANDEBOL","TURBO DE MOTOR","PARÁBOLA","ALIADA","PACOTE DE DADOS","ESPUMA","TIJOLOS","PATADA LATERAL"};
        // Exact control vertices from Godot 0.6 kart.gd / kart_ufn_track.gd.
        public static KartPoint[] TrackAnchors(int id)
        {
            float[] data;
            switch(id)
            {
                case 0:data=new float[]{-54,-46,0,-52,57,-46,76,-16,50,10,72,44,28,64,-16,53,-57,65,-79,30,-59,2,-78,-26};break;
                case 1:data=new float[]{-50,-60,10,-62,65,-49,83,-4,58,29,18,20,0,66,-55,64,-84,30,-68,0};break;
                case 2:data=new float[]{-65,-63,28,-67,76,-50,78,-7,28,-2,26,48,65,65,-18,72,-73,51,-77,0};break;
                case 3:data=new float[]{-60,-68,0,-77,70,-65,83,-20,45,7,75,60,13,77,-26,40,-74,62,-86,13,-44,-5};break;
                case 4:data=new float[]{-52,66,32,66,70,63,86,47,86,-40,78,-60,58,-66,-61,-66,-84,-54,-90,-34,-90,42,-77,61};break;
                default:throw new ArgumentOutOfRangeException(nameof(id));
            }
            var result=new KartPoint[data.Length/2];for(int i=0;i<result.Length;i++)result[i]=new KartPoint(data[i*2],data[i*2+1]);return result;
        }
    }

    [Serializable]
    public sealed class KartAppearance
    {
        public string Reference,Name,Kart,Accent,Rim,Shirt,Pants,Shoes,Hair,Style,HairStyle,Glasses="none",Beard="none",Detail="none",Skin="e0af89",Inner="222832";
        public float Width=1;
        private KartAppearance(string reference,string kart,string accent,string rim,string shirt,string pants,string shoes,string hair,string style,string hairstyle)
        {Reference=reference;Kart=kart;Accent=accent;Rim=rim;Shirt=shirt;Pants=pants;Shoes=shoes;Hair=hair;Style=style;HairStyle=hairstyle;}
        public static readonly KartAppearance[] All={
            new KartAppearance("arthur","202127","e7e8e5","bdc0c5","181a1e","202126","dedfdc","442d26","hoodie","curls"){Detail="straps"},
            new KartAppearance("vitorbem","123c70","078cda","168acd","1c304b","87a8c0","e0dfd9","493026","puffer","curls"){Inner="60656b"},
            new KartAppearance("maria","125be9","f0f1e9","1a64e8","10294d","8cabc9","191c23","241a23","tee","long"){Width=.87f},
            new KartAppearance("diogo","edece7","c91426","d81a27","ede9e1","252428","ca1525","4a3027","tee","messy"){Detail="splatter"},
            new KartAppearance("mirkos","173f72","097bd4","197ac9","243a60","181f2d","24272c","32262b","blazer","short"){Glasses="clear",Beard="goatee",Detail="lanyard",Skin="cb946e"},
            new KartAppearance("felipe","c61722","ebe8dd","be1420","66636a","d6ccba","c8242d","613b27","open_hoodie","messy"){Detail="chain"},
            new KartAppearance("murilo","ea88b2","f8e6ef","ed87b8","e7a7c6","aaa99e","e2e2dc","261e22","hoodie","spiky"){Glasses="clear",Detail="cords"},
            new KartAppearance("gabriel","232326","c9a044","ceac5d","181c22","202227","e5e5df","392b25","hoodie","swept"){Detail="chest_mark"},
            new KartAppearance("fabiano","68273e","af4766","8c3a53","6e2d40","284760","5c463a","664b3f","sweater","receding"){Beard="stubble",Detail="collar",Skin="dbab86"},
            new KartAppearance("vitormal","151d31","075ce3","0c75ea","22242a","88a7c6","d8dbdb","37251f","hoodie","curls"){Detail="zip"},
            new KartAppearance("lorenzo","0759d4","e9e9e3","196ee2","1c2027","20242b","161e24","51392b","tracksuit","buzz"){Glasses="dark",Beard="full",Detail="double_stripe",Inner="16446b",Skin="c28f6b",Width=1.08f},
            new KartAppearance("leonardo","304b86","e1e5e6","6d85bf","246447","335582","bfc3c3","7f8384","sweater","bald"){Glasses="clear",Beard="gray",Skin="d6a17b",Width=1.18f},
            new KartAppearance("cristian","075ec9","e8e8e2","1978df","252629","d2cab8","23272d","241d20","puffer","fade"){Glasses="clear",Beard="full",Skin="c99572"},
            new KartAppearance("fernando","20252e","2f80db","4277b5","a1a1a0","294b70","20272d","29201d","hoodie","swept"){Beard="stubble",Detail="cords",Skin="d3a07c"},
            new KartAppearance("diogo_alt","cc1724","eeebe3","cf1c27","1d1e21","202225","191d22","865742","shorts_tee","bald"){Skin="c58967",Glasses="dark",Detail="tattoos",Width=1.14f}
        };
        public static KartAppearance Get(int character,int skin=0)=>All[character==3&&skin>0?14:Math.Max(0,Math.Min(13,character))];
    }
}
