/// The signed-in account (`UserDto` from `/api/v1/auth/login` and `/api/v1/auth/me`).
class AppUser {
  AppUser({
    required this.id,
    required this.studentCode,
    required this.fullName,
    required this.email,
    required this.phone,
    required this.role,
    required this.campus,
    required this.academicYear,
    required this.classId,
    required this.classCode,
    required this.className,
    required this.isClassMonitor,
    required this.accumulatedPoints,
  });

  final int id;
  final String studentCode;
  final String fullName;
  final String email;
  final String phone;
  final String role;
  final String campus;
  final String academicYear;
  final int? classId;
  final String? classCode;
  final String? className;
  final bool isClassMonitor;
  final int accumulatedPoints;

  /// The Mobile app is for students and class monitors (US-30); staff use the Web.
  bool get isStudentRole => role == 'Student' || role == 'Monitor';
  bool get isMonitor => role == 'Monitor';
  String get roleLabel => isMonitor ? 'Lớp trưởng' : 'Sinh viên';

  factory AppUser.fromJson(Map<String, dynamic> json) => AppUser(
        id: json['id'] as int,
        studentCode: json['studentCode'] as String? ?? '',
        fullName: json['fullName'] as String? ?? '',
        email: json['email'] as String? ?? '',
        phone: json['phone'] as String? ?? '',
        role: json['role'] as String? ?? '',
        campus: json['campus'] as String? ?? '',
        academicYear: json['academicYear'] as String? ?? '',
        classId: json['classId'] as int?,
        classCode: json['classCode'] as String?,
        className: json['className'] as String?,
        isClassMonitor: json['isClassMonitor'] as bool? ?? false,
        accumulatedPoints: json['accumulatedPoints'] as int? ?? 0,
      );
}
