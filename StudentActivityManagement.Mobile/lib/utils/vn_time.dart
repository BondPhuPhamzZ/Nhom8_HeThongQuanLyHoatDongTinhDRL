import 'package:intl/intl.dart';

/// The API speaks UTC (`...Z`). Like the Web, the app shows Vietnam time (UTC+7) on every device,
/// whatever the phone's own time zone is.
abstract final class VnTime {
  static const _offset = Duration(hours: 7);
  static final _dateTime = DateFormat('HH:mm dd/MM/yyyy');
  static final _date = DateFormat('dd/MM/yyyy');
  static final _time = DateFormat('HH:mm');

  static DateTime toVn(DateTime utc) => utc.toUtc().add(_offset);

  static String dateTime(DateTime? utc) => utc == null ? '—' : _dateTime.format(toVn(utc));

  static String date(DateTime? utc) => utc == null ? '—' : _date.format(toVn(utc));

  static String time(DateTime? utc) => utc == null ? '—' : _time.format(toVn(utc));

  /// Parses an API timestamp; values without an offset are treated as UTC, as the API contract says.
  static DateTime parse(Object? value) {
    final text = value?.toString() ?? '';
    final parsed = DateTime.tryParse(text);
    if (parsed == null) return DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
    return parsed.isUtc ? parsed : DateTime.utc(parsed.year, parsed.month, parsed.day, parsed.hour, parsed.minute, parsed.second);
  }

  static DateTime? parseOrNull(Object? value) => value == null ? null : parse(value);
}
