namespace Cirreum.Demo.Client;

using System.Text;

public static class LorumIpsum {

	public static string Generate(int minWords, int maxWords, int minSentences, int maxSentences, int numLines) {

		var words = new[] { "lorem", "ipsum", "dolor", "sit", "amet", "consectetuer", "adipiscing", "elit", "sed", "diam", "nonummy", "nibh", "euismod", "tincidunt", "ut", "laoreet", "dolore", "magna", "aliquam", "erat" };

		var rand = new Random();
		var numSentences = rand.Next(maxSentences - minSentences)
			+ minSentences;
		var numWords = rand.Next(maxWords - minWords) + minWords;

		var sb = new StringBuilder();
		for (var p = 0; p < numLines; p++) {
			for (var s = 0; s < numSentences; s++) {
				for (var w = 0; w < numWords; w++) {
					if (w > 0) { sb.Append(' '); }
					var word = words[rand.Next(words.Length)];
					if (w == 0) { word = string.Concat(word[..1].Trim().ToUpper(), word.AsSpan(1)); }
					sb.Append(word);
				}
				sb.Append(". ");
			}
			if (p < numLines - 1) {
				sb.AppendLine();
			}
		}
		return sb.ToString();
	}

}