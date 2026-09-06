// Official Blazor file-download pattern: a .NET stream is handed to the browser as a Blob and saved through
// a temporary, invisible <a download> link. Used by Components/Production/OrdersGrid.razor to save an
// order's downloaded JSON file.
export async function downloadFileFromStream(fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: "application/json" });
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName ?? "";
    anchor.click();
    anchor.remove();

    URL.revokeObjectURL(url);
}
