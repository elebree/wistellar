/**
 * Ends the startup screen from app.html: fades it out if it is showing, then removes it so its
 * animations stop and it never shows later. Safe to call more than once.
 */
export function hideSplash() {
  const splash = document.getElementById("splash");
  if (!splash || splash.classList.contains("done")) return;
  splash.classList.add("done");
  setTimeout(() => splash.remove(), 500);
}
