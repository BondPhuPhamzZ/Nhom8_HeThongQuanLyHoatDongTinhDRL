import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import '../constants/api_config.dart';
import 'preference_service.dart';

/// An error the user should see: the API's ProblemDetails `detail`/`title`, or a connection problem.
class ApiException implements Exception {
  ApiException(this.message, {this.statusCode, this.errorCode, this.fieldErrors = const {}});

  final String message;
  final int? statusCode;
  final String? errorCode;
  final Map<String, List<String>> fieldErrors;

  bool get isUnauthorized => statusCode == 401;

  /// The message plus field validation errors, for a single snackbar or form banner.
  String get fullMessage {
    final details = fieldErrors.values.expand((items) => items).where((item) => item.isNotEmpty).toSet();
    return details.isEmpty ? message : '$message\n${details.join('\n')}';
  }

  @override
  String toString() => message;
}

/// One HTTP entry point for every service, the Mobile counterpart of the Web's ApiClient: attaches the JWT,
/// unwraps `ApiResponseDto.data` and turns ProblemDetails into [ApiException].
class ApiClient {
  ApiClient._();
  static final instance = ApiClient._();

  final http.Client _http = http.Client();

  /// Called when the API rejects the token (expired, account locked or role changed).
  void Function()? onUnauthorized;

  String get baseUrl => PreferenceService.instance.baseUrl;

  Uri uri(String path, [Map<String, Object?> query = const {}]) {
    final params = <String, String>{
      for (final entry in query.entries)
        if (entry.value != null && entry.value.toString().isNotEmpty) entry.key: entry.value.toString(),
    };
    return Uri.parse('$baseUrl$path').replace(queryParameters: params.isEmpty ? null : params);
  }

  Map<String, String> _headers({bool json = true}) {
    final token = PreferenceService.instance.token;
    return {
      'Accept': 'application/json',
      if (json) 'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };
  }

  Future<ApiResult> get(String path, [Map<String, Object?> query = const {}]) =>
      _send(() => _http.get(uri(path, query), headers: _headers()));

  Future<ApiResult> post(String path, [Object? body]) =>
      _send(() => _http.post(uri(path), headers: _headers(), body: body == null ? null : jsonEncode(body)));

  /// POST allowed to take longer than [ApiConfig.requestTimeout]: one assistant question (US-31) may need
  /// several model round trips on the API side.
  Future<ApiResult> postLong(String path, Object? body, Duration timeout) => _send(
        () => _http.post(uri(path), headers: _headers(), body: body == null ? null : jsonEncode(body)),
        timeout: timeout,
      );

  Future<ApiResult> patch(String path, [Object? body]) =>
      _send(() => _http.patch(uri(path), headers: _headers(), body: body == null ? null : jsonEncode(body)));

  Future<ApiResult> delete(String path) => _send(() => _http.delete(uri(path), headers: _headers()));

  Future<ApiResult> _send(Future<http.Response> Function() call, {Duration? timeout}) async {
    final http.Response response;
    try {
      response = await call().timeout(timeout ?? ApiConfig.requestTimeout);
    } on TimeoutException {
      throw ApiException('API không phản hồi. Kiểm tra kết nối mạng và địa chỉ máy chủ.');
    } on Exception {
      throw ApiException('Không kết nối được API tại $baseUrl. Kiểm tra API đang chạy và địa chỉ máy chủ trong phần cài đặt.');
    }

    final text = utf8.decode(response.bodyBytes);
    Object? decoded;
    try {
      decoded = text.isEmpty ? null : jsonDecode(text);
    } on FormatException {
      decoded = null;
    }
    final body = decoded is Map<String, dynamic> ? decoded : const <String, dynamic>{};

    if (response.statusCode >= 200 && response.statusCode < 300) {
      return ApiResult(response.statusCode, body['data'], (body['message'] as String?) ?? '');
    }

    final error = ApiException(
      (body['detail'] as String?) ?? (body['title'] as String?) ?? _fallbackMessage(response.statusCode),
      statusCode: response.statusCode,
      errorCode: body['errorCode'] as String?,
      fieldErrors: _fieldErrors(body['errors']),
    );
    if (error.isUnauthorized && PreferenceService.instance.token != null) {
      onUnauthorized?.call();
    }
    throw error;
  }

  static Map<String, List<String>> _fieldErrors(Object? raw) {
    if (raw is! Map) return const {};
    return {
      for (final entry in raw.entries)
        entry.key.toString(): entry.value is List ? (entry.value as List).map((item) => item.toString()).toList() : [entry.value.toString()],
    };
  }

  static String _fallbackMessage(int status) => switch (status) {
        401 => 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
        403 => 'Bạn không có quyền thực hiện thao tác này.',
        404 => 'Không tìm thấy dữ liệu.',
        _ => 'Có lỗi xảy ra. Vui lòng thử lại.',
      };
}

/// Successful response: `data` from ApiResponseDto and its Vietnamese `message`.
class ApiResult {
  ApiResult(this.statusCode, this.data, this.message);

  final int statusCode;
  final Object? data;
  final String message;

  Map<String, dynamic> get map => (data as Map).cast<String, dynamic>();
  List<dynamic> get list => (data as List?) ?? const [];
}
