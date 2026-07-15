(function() {
  try {
    var themeStorage = localStorage.getItem('theme-storage');
    var theme = 'ethiopian';
    if (themeStorage) {
      try {
        var parsed = JSON.parse(themeStorage);
        if (parsed && typeof parsed === 'object' && parsed.state && typeof parsed.state.theme === 'string') {
          var themeValue = String(parsed.state.theme);
          if (['light', 'dark', 'ethiopian', 'system'].indexOf(themeValue) !== -1) theme = themeValue;
        }
      } catch (e) {}
    }
    var root = document.documentElement;
    root.classList.remove('light', 'dark', 'ethiopian');
    if (theme === 'system') {
      var systemTheme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
      root.classList.add(systemTheme);
    } else {
      root.classList.add(theme);
    }
  } catch (e) {
    document.documentElement.classList.add('ethiopian');
  }
})();
