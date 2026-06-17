window.rookRunDownloads = window.rookRunDownloads || {};

window.rookRunDownloads.downloadTextFile = (fileName, content, mimeType) => {
    const blob = new Blob([content], { type: mimeType || "text/plain;charset=utf-8" });
    const link = document.createElement("a");
    const url = URL.createObjectURL(blob);

    link.href = url;
    link.download = fileName;
    link.style.display = "none";

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    URL.revokeObjectURL(url);
};
