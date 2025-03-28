using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniprojectCross.Model;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace MiniprojectCross.ViewModel
{
    public partial class ShowDataStudent : ObservableObject
    {
        [ObservableProperty]
        private User user;

        [ObservableProperty]
        private PreviousSemester selectedPreviousSemester;

        [ObservableProperty]
        private ObservableCollection<AvailableCourse> inMemoryAvailableCourses;

        [ObservableProperty]
        private ObservableCollection<CurrentSemesterSubject> inMemoryRegisteredCourses;

        [ObservableProperty]
        private ObservableCollection<AvailableCourse> filteredAvailableCourses;

        [ObservableProperty]
        private string searchText;

        [ObservableProperty]
        private long totalRegisteredCredits; // เปลี่ยนเป็น ObservableProperty เพื่อให้ UI ติดตามการเปลี่ยนแปลงได้ดีขึ้น

        public ObservableCollection<CurrentSemesterSubject> CurrentSemesterSubjects =>
            new ObservableCollection<CurrentSemesterSubject>(User?.CurrentSemester?.Subjects ?? new List<CurrentSemesterSubject>());

        public ObservableCollection<PreviousSemesterSubject> PreviousSemesterSubjects =>
            new ObservableCollection<PreviousSemesterSubject>(SelectedPreviousSemester?.Subjects ?? new List<PreviousSemesterSubject>());

        public string FullName => User?.Student?.Profile != null ?
            $"{User.Student.Profile.Firstname} {User.Student.Profile.Lastname}" : string.Empty;

        public string CurrentSemesterTermDisplay =>
            User?.CurrentSemester != null ?
            $"ภาคการศึกษา {User.CurrentSemester.Term}/{User.CurrentSemester.DisplayAcademicYear}" :
            "ภาคการศึกษา N/A";

        public string SelectedPreviousSemesterDisplay =>
            SelectedPreviousSemester != null ?
            $"ภาคการศึกษา {SelectedPreviousSemester.Term}/{SelectedPreviousSemester.DisplayAcademicYear}" :
            "ภาคการศึกษา N/A";

        public string PreviousSemester1Display =>
            User?.PreviousSemesters?.Count > 0 ?
            $"{User.PreviousSemesters[0].Term}/{User.PreviousSemesters[0].DisplayAcademicYear}" :
            "N/A";

        public string PreviousSemester2Display =>
            User?.PreviousSemesters?.Count > 1 ?
            $"{User.PreviousSemesters[1].Term}/{User.PreviousSemesters[1].DisplayAcademicYear}" :
            "N/A";

        public string PreviousSemester3Display =>
            User?.PreviousSemesters?.Count > 2 ?
            $"{User.PreviousSemesters[2].Term}/{User.PreviousSemesters[2].DisplayAcademicYear}" :
            "N/A";

        public ShowDataStudent()
        {
            LoadDataAsync();
        }

        async Task LoadDataAsync()
        {
            var user = await ReadJsonAsync();
            if (user != null)
            {
                User = user;
                SelectedPreviousSemester = User.PreviousSemesters?.FirstOrDefault();

                InMemoryRegisteredCourses = new ObservableCollection<CurrentSemesterSubject>(
                    User.CurrentSemester?.Subjects ?? new List<CurrentSemesterSubject>());
                System.Diagnostics.Debug.WriteLine($"Loaded {InMemoryRegisteredCourses.Count} registered courses.");

                var registeredCourseIdsAndSections = InMemoryRegisteredCourses
                    .Select(s => (s.Id, s.Section))
                    .ToHashSet();
                InMemoryAvailableCourses = new ObservableCollection<AvailableCourse>(
                    (User.AvailableCourses ?? new List<AvailableCourse>())
                        .Where(c => !registeredCourseIdsAndSections.Contains((c.Id, c.Section)))
                        .ToList());
                System.Diagnostics.Debug.WriteLine($"Loaded {InMemoryAvailableCourses.Count} available courses.");

                FilteredAvailableCourses = new ObservableCollection<AvailableCourse>(InMemoryAvailableCourses);

                UpdateTotalRegisteredCredits(); // อัปเดตหน่วยกิตตอนโหลดข้อมูล

                OnPropertyChanged(nameof(CurrentSemesterSubjects));
                OnPropertyChanged(nameof(PreviousSemesterSubjects));
                OnPropertyChanged(nameof(FullName));
                OnPropertyChanged(nameof(CurrentSemesterTermDisplay));
                OnPropertyChanged(nameof(PreviousSemester1Display));
                OnPropertyChanged(nameof(PreviousSemester2Display));
                OnPropertyChanged(nameof(PreviousSemester3Display));
                OnPropertyChanged(nameof(SelectedPreviousSemesterDisplay));
            }
        }

        [RelayCommand]
        public void SelectSemester(PreviousSemester semester)
        {
            SelectedPreviousSemester = semester;
            OnPropertyChanged(nameof(PreviousSemesterSubjects));
            OnPropertyChanged(nameof(PreviousSemester1Display));
            OnPropertyChanged(nameof(PreviousSemester2Display));
            OnPropertyChanged(nameof(PreviousSemester3Display));
            OnPropertyChanged(nameof(SelectedPreviousSemesterDisplay));
        }

        [RelayCommand]
        public void RegisterCourse(AvailableCourse course)
        {
            System.Diagnostics.Debug.WriteLine($"Registering: {course?.Name}");
            if (course == null) return;

            InMemoryAvailableCourses.Remove(course);
            FilteredAvailableCourses.Remove(course);
            var subject = course.ToCurrentSemesterSubject();
            InMemoryRegisteredCourses.Add(subject);
            System.Diagnostics.Debug.WriteLine($"Added {subject.Name} to InMemoryRegisteredCourses. Total: {InMemoryRegisteredCourses.Count}");

            SyncDataToUser();
            SaveDataToJsonAsync();
            UpdateTotalRegisteredCredits(); // อัปเดตหน่วยกิต
        }

        [RelayCommand]
        public async Task WithdrawCourse(CurrentSemesterSubject subject)
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to withdraw: {subject?.Name ?? "null"}");
            if (subject == null)
            {
                System.Diagnostics.Debug.WriteLine("Subject is null, cannot withdraw.");
                return;
            }

            bool confirm = await Application.Current.MainPage.DisplayAlert(
                "ยืนยันการถอนวิชา",
                $"คุณต้องการถอนวิชา {subject.Name} หรือไม่?",
                "ตกลง",
                "ยกเลิก");
            if (!confirm)
            {
                System.Diagnostics.Debug.WriteLine("User canceled withdrawal.");
                return;
            }

            var subjectToRemove = InMemoryRegisteredCourses.FirstOrDefault(s =>
                s.Id == subject.Id && s.Section == subject.Section);
            if (subjectToRemove != null)
            {
                InMemoryRegisteredCourses.Remove(subjectToRemove);
                System.Diagnostics.Debug.WriteLine($"Removed {subjectToRemove.Name} from InMemoryRegisteredCourses. Total: {InMemoryRegisteredCourses.Count}");

                var course = new AvailableCourse
                {
                    Id = subjectToRemove.Id,
                    Name = subjectToRemove.Name,
                    NameEng = subjectToRemove.NameEng,
                    Section = subjectToRemove.Section,
                    Credits = subjectToRemove.Credits,
                    Instructor = subjectToRemove.Instructor,
                    Schedule = subjectToRemove.Schedule,
                    MidtermExam = subjectToRemove.MidtermExam,
                    FinalExam = subjectToRemove.FinalExam,
                    AvailableSeats = 1,
                    MaxSeats = 40,
                    Prerequisite = new List<string>()
                };

                InMemoryAvailableCourses.Add(course);
                FilteredAvailableCourses.Add(course);
                System.Diagnostics.Debug.WriteLine($"Added {course.Name} back to InMemoryAvailableCourses. Total: {InMemoryAvailableCourses.Count}");

                SyncDataToUser();
                await SaveDataToJsonAsync();
                UpdateTotalRegisteredCredits(); // อัปเดตหน่วยกิต

                OnPropertyChanged(nameof(InMemoryRegisteredCourses)); // อัปเดต UI
                await Application.Current.MainPage.DisplayAlert(
                    "สำเร็จ",
                    $"ถอนวิชา {subject.Name} เรียบร้อยแล้ว",
                    "ตกลง");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Subject {subject.Id} (Section {subject.Section}) not found in InMemoryRegisteredCourses.");
                await Application.Current.MainPage.DisplayAlert(
                    "ข้อผิดพลาด",
                    $"ไม่พบวิชา {subject.Name} ในรายการที่ลงทะเบียน",
                    "ตกลง");
            }
        }

        [RelayCommand]
        public void SearchCourses()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredAvailableCourses = new ObservableCollection<AvailableCourse>(InMemoryAvailableCourses);
            }
            else
            {
                var filtered = InMemoryAvailableCourses
                    .Where(c => c.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                c.NameEng.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                c.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                FilteredAvailableCourses = new ObservableCollection<AvailableCourse>(filtered);
            }
        }

        async Task<User> ReadJsonAsync()
        {
            try
            {
                string filePath = Path.Combine(FileSystem.AppDataDirectory, "data.json");
                if (!File.Exists(filePath))
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync("data.json");
                    using var reader = new StreamReader(stream);
                    var contents = await reader.ReadToEndAsync();
                    await File.WriteAllTextAsync(filePath, contents);
                    System.Diagnostics.Debug.WriteLine("Copied initial data.json to AppDataDirectory.");
                }

                var jsonContent = await File.ReadAllTextAsync(filePath);
                return User.FromJson(jsonContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return null;
            }
        }

        private void SyncDataToUser()
        {
            if (User == null) return;

            User.CurrentSemester.Subjects = InMemoryRegisteredCourses.ToList();
            User.AvailableCourses = InMemoryAvailableCourses.ToList();
            System.Diagnostics.Debug.WriteLine($"Synced data: {User.CurrentSemester.Subjects.Count} registered, {User.AvailableCourses.Count} available.");
        }

        private async Task SaveDataToJsonAsync()
        {
            try
            {
                string filePath = Path.Combine(FileSystem.AppDataDirectory, "data.json");
                string jsonString = JsonSerializer.Serialize(User, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, jsonString);
                System.Diagnostics.Debug.WriteLine($"Saved data to {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving JSON: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "ข้อผิดพลาด",
                    $"ไม่สามารถบันทึกข้อมูล: {ex.Message}",
                    "ตกลง");
            }
        }

        // ฟังก์ชันใหม่เพื่ออัปเดต TotalRegisteredCredits
        private void UpdateTotalRegisteredCredits()
        {
            TotalRegisteredCredits = InMemoryRegisteredCourses?.Sum(s => s.Credits) ?? 0;
            System.Diagnostics.Debug.WriteLine($"Updated TotalRegisteredCredits: {TotalRegisteredCredits}");
        }
    }
}