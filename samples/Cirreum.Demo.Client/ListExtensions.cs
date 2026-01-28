namespace Cirreum.Demo.Client;

public static class ListExtensions {

	private static readonly Random random = new Random();

	public static T GetRandomElementRecursive<T>(this List<T> list) where T : class {
		var totalCount = GetTotalCount(list);
		var randomIndex = random.Next(0, totalCount);
		return GetRandomElementRecursiveHelper(list, ref randomIndex);
	}

	private static T GetRandomElementRecursiveHelper<T>(List<T> list, ref int randomIndex) where T : class {

		foreach (var item in list) {
			var childrenProperty = typeof(T).GetProperty("Children");
			if (childrenProperty != null) {
				if (childrenProperty.GetValue(item) is List<T> children && children.Count > 0) {
					var childrenCount = GetTotalCount(children);
					if (randomIndex < childrenCount) {
						return GetRandomElementRecursiveHelper(children, ref randomIndex);
					}
					randomIndex -= childrenCount;
				}
			}

			if (randomIndex == 0) {
				return item;
			}
			randomIndex--;
		}

		// This should never happen if our counting is correct
		throw new InvalidOperationException("Unexpected error in random selection.");
	}

	private static int GetTotalCount<T>(List<T> list) where T : class {
		var count = list.Count;
		foreach (var item in list) {
			var childrenProperty = typeof(T).GetProperty("Children");
			if (childrenProperty != null) {
				if (childrenProperty.GetValue(item) is List<T> children) {
					count += GetTotalCount(children);
				}
			}
		}
		return count;
	}
}