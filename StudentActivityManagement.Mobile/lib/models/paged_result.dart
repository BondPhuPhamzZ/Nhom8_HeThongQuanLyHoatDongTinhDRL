/// `PagedResultDto<T>` from the API.
class PagedResult<T> {
  PagedResult({required this.items, required this.totalCount, required this.pageIndex, required this.pageSize});

  final List<T> items;
  final int totalCount;
  final int pageIndex;
  final int pageSize;

  bool get hasNextPage => pageIndex * pageSize < totalCount;

  factory PagedResult.fromJson(Map<String, dynamic> json, T Function(Map<String, dynamic>) item) => PagedResult(
        items: ((json['items'] as List?) ?? const []).map((raw) => item((raw as Map).cast<String, dynamic>())).toList(),
        totalCount: json['totalCount'] as int? ?? 0,
        pageIndex: json['pageIndex'] as int? ?? 1,
        pageSize: json['pageSize'] as int? ?? 10,
      );
}
