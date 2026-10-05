mergeInto(LibraryManager.library, {
  MvvmReport: function(passed, message) {
    window.__mvvmResult = { passed: passed, message: UTF8ToString(message) };
  }
});
