namespace Cirreum.Demo.Client;

using System;
using System.Text;

public class RandomSentenceGenerator {

	private static readonly Random random = new Random();
	private static readonly string[] words = [
		"the", "be", "to", "of", "and", "a", "in", "that", "have", "I", "it", "for", "not", "on", "with",
		"he", "as", "you", "do", "at", "this", "but", "his", "by", "from", "they", "we", "say", "her", "she",
		"or", "an", "will", "my", "one", "all", "would", "there", "their", "what", "so", "up", "out", "if",
		"about", "who", "get", "which", "go", "me", "when", "make", "can", "like", "time", "no", "just",
		"him", "know", "take", "people", "into", "year", "your", "good", "some", "could", "them", "see",
		"other", "than", "then", "now", "look", "only", "come", "its", "over", "think", "also", "back"
	];

	public static string Generate() {
		// Determine random length between 80-100 characters
		var targetLength = random.Next(80, 101);
		var sentence = new StringBuilder();

		// Add words until we reach or exceed the target length
		while (sentence.Length < targetLength) {
			// Add a space if this isn't the first word
			if (sentence.Length > 0) {
				sentence.Append(' ');
			}

			// Add a random word
			var word = words[random.Next(words.Length)];

			// Randomly capitalize the first letter (for sentence beginnings)
			if (sentence.Length == 0 || random.Next(15) == 0) {
				word = char.ToUpper(word[0]) + word[1..];
			}

			// Randomly add punctuation
			if (random.Next(10) == 0 && sentence.Length > 20) {
				word += random.Next(4) switch {
					0 => ",",
					1 => ".",
					2 => "?",
					_ => "!"
				};
			}

			sentence.Append(word);
		}

		// Ensure the sentence ends with proper punctuation
		var lastChar = sentence[^1];
		if (lastChar != '.' && lastChar != '?' && lastChar != '!') {
			sentence.Append('.');
		}

		return sentence.ToString();
	}
}