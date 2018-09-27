using System.Collections;
using System.Configuration;

namespace proto_excel
{
	/*
	 * 支持{key}替换
	 */
	class Config
	{
		public IDictionary dic;

		public Config(string section)
		{
			dic = (IDictionary)ConfigurationManager.GetSection(section);
			if (null == dic)
				return;
			
			string[] keys = new string[dic.Count];
			dic.Keys.CopyTo(keys, 0);
			foreach (string key in keys)
				Replace(key);
		}

		public string Replace(string key)
		{
			string str = (string)dic[key];
			int p0 = 0;
			int p1 = -1;
			bool change = false;
			while (-1 != (p1 = str.IndexOf('{', p0)))
			{
				int p2 = FindPair(str, '{', p1 + 1);
				string innerKey = p2 - p1 - 1 > 0 ? str.Substring(p1 + 1, p2 - p1 - 1) : null;
				if (null != innerKey)
				{
					string innerStr = Replace(innerKey);
					str = str.Substring(0, p1) + innerStr + str.Substring(p2 + 1);
					p0 = p1 + innerStr.Length;
					change = true;
				}
				else
					p0 = p1 + 1;
			}
			if (change)
				dic[key] = str;
			return str;
		}

		public int FindPair(string s, char p0, int i)
		{
			char p1 = ' ';
			switch (p0)
			{
				case '{': p1 = '}'; break;
				case '(': p1 = ')'; break;
				case '[': p1 = ']'; break;
			}

			int depth = 0;
			while (i < s.Length)
			{
				if (s[i] == p1)
				{
					if (depth == 0)
						return i;
					else
						depth--;
				}
				else if (s[i] == p0)
					depth++;
				i++;
			}
			return -1;
		}

		public string this[string key]
		{
			get { return (string)dic[key]; }
			set { dic[key] = value; }
		}
	}
}
