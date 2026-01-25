var definitionsApp = (function () {

    var init = function () {
        registerTabEvents();

        // Load the default tab (Brands)
        if (typeof brandApp !== 'undefined') {
            brandApp.init();
        }
    };

    var registerTabEvents = function () {
        // Handle "New" Button Click - Dispatch to active module
        $('#btnAddNew').on('click', function () {
            var activeTab = $('.tab-pane.active').attr('id');

            if (activeTab === 'brands-pane') {
                brandApp.openCreatePanel();
            } else if (activeTab === 'categories-pane') {
                alert("Category module coming soon.");
                // categoryApp.openCreatePanel();
            } else if (activeTab === 'uom-pane') {
                alert("UoM module coming soon.");
                // uomApp.openCreatePanel();
            }
        });

        // Handle Tab Switching - Update Button Text & Adjust DataTables
        $('button[data-bs-toggle="tab"]').on('shown.bs.tab', function (e) {
            var target = $(e.target).attr('id'); // active tab

            if (target === 'brands-tab') {
                $('#btnAddNewText').text('New Brand');
                // DataTables fix for hidden tabs
                $('#tblBrands').DataTable().columns.adjust().draw();
            }
            else if (target === 'categories-tab') {
                $('#btnAddNewText').text('New Category');
                // init Category DataTables here if needed
            }
            else if (target === 'uom-tab') {
                $('#btnAddNewText').text('New Unit');
            }
        });
    };

    return {
        init: init
    };
})();

$(document).ready(function () {
    definitionsApp.init();
});