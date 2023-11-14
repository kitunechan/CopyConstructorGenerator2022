using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project1 {
	class Class1 {
		/// <summary>
		/// コピーコンストラクタ
		/// </summary>
		public Class1( Class1 value ) {
			this.w = value.w;
			this.num1 = value.num1;
			this.GetOnly = value.GetOnly;
			this.test = value.test;
			this.prop = value.prop;
			this.num = value.num;
			this.flag = value.flag;
			this.class1 = new Class1( value.class1 );
			this.list = value.list.ToList();
		}

		public Class1() {

		}

		public W w { get; } = W.a;

		public int? num1 { get; set; }
		public string GetOnly { get; } = "getto";


		public static string _static = "static";

		const string constString = "こんすと";

		public List<List<List<Class1>>> _multiList;

		public string test { get; set; }
		public double prop { get; set; }

		private int num { get; set; }

		private bool flag { get; set; }

		public string str;
		private string privateStr;

		public Class1 class1 { get; set; }

		public List<string> list { get; set; }

		public Dictionary<string, int> _dic;
		public Dictionary<Dictionary<List<int>, List<string>>, int> _dic2;

		Action<Class1> action;

	}


	enum W {
		a, b, c, d
	}
}
