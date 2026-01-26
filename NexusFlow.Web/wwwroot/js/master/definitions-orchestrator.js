var definitionsApp = (function () {

    var init = function () {
        registerTabEvents();

        // Initialize Default Tab (Brands)
        if (typeof brandApp !== 'undefined') {
            brandApp.init();
        }
    };

    var registerTabEvents = function () {
        // Handle Tab Switching (UI Updates for DataTables)
        // This is critical because DataTables cannot calculate column widths inside hidden tabs.
        var triggerTabList = [].slice.call(document.querySelectorAll('#attributeTabs button'))
        triggerTabList.forEach(function (triggerEl) {

            triggerEl.addEventListener('shown.bs.tab', function (event) {
                var targetId = event.target.getAttribute('data-bs-target');

                if (targetId === '#brands-pane') {
                    $('#tblBrands').DataTable().columns.adjust().responsive.recalc();
                }
                else if (targetId === '#categories-pane') {
                    if (typeof categoryApp !== 'undefined') categoryApp.init();
                    $('#tblCategories').DataTable().columns.adjust().responsive.recalc();
                }
                else if (targetId === '#uom-pane') {
                    if (typeof unitApp !== 'undefined') unitApp.init();
                    $('#tblUnits').DataTable().columns.adjust().responsive.recalc();
                }
            })
        });
    };

    return {
        init: init
    };
})();

$(document).ready(function () {
    definitionsApp.init();
});