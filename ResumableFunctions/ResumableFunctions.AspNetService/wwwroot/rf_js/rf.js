//used in services view
function gotoViewPart(index, url) {
    if (index == undefined)
        index = '0';
    var i, tablinks;

    //de-activate all menu items
    tablinks = document.getElementsByClassName("tablink");
    for (i = 0; i < tablinks.length; i++) {
        tablinks[i].className = tablinks[i].className.replace(" w3-indigo", "");
    }

    var currentButton = document.getElementById('tab-link-' + index);
    currentButton.className += " w3-indigo";
    setMainPageView(url, title(index))
    
    window.location.hash = `#view=${index}&${url.split('?')[1]}`;
}

function searchFunctions() {
    var serviceId = document.getElementById("selectedService").value;
    var functionSearchTerm = document.getElementById("functionSearchTerm").value;
    setMainPageView(
        `/RF/Home/_ResumableFunctionsList?serviceId=${serviceId}&searchTerm=${functionSearchTerm}`, title(1));
    setOrUpdateHashParameter('serviceId', serviceId);
    setOrUpdateHashParameter('searchTerm', functionSearchTerm);
}

function resetFunctionsView() {
    setMainPageView(`/RF/Home/_ResumableFunctionsList`, title(1));
    window.location.hash = `#view=1`;
}

function searchMethodGroups() {
    var serviceId = document.getElementById("selectedService").value;
    var searchTerm = document.getElementById("searchTerm").value;
    setMainPageView(`/RF/Home/_MethodGroups?serviceId=${serviceId}&searchTerm=${searchTerm}`, title(2));
    setOrUpdateHashParameter('serviceId', serviceId);
    setOrUpdateHashParameter('searchTerm', searchTerm);
}

function resetMethodGroups() {
    setMainPageView(`/RF/Home/_MethodGroups`, title(2));
    window.location.hash = `#view=2`;
}

function searchPushedCalls() {
    var serviceId = document.getElementById("selectedService").value;
    var searchTerm = document.getElementById("searchTerm").value;
    setMainPageView(`/RF/Home/_PushedCalls?serviceId=${serviceId}&searchTerm=${searchTerm}`, title(3));
    setOrUpdateHashParameter('serviceId', serviceId);
    setOrUpdateHashParameter('searchTerm', searchTerm);
}

function resetPushedCalls() {
    setMainPageView(`/RF/Home/_PushedCalls`, title(3));
    window.location.hash = `#view=3`;
}

function searchLogs() {
    var serviceId = document.getElementById("selectedService").value;
    var statusCode = document.getElementById("selectedStatusCode").value;
    setMainPageView(`/RF/Home/_LatestLogs?serviceId=${serviceId}&statusCode=${statusCode}`, title(4));
    setOrUpdateHashParameter('serviceId', serviceId);
    setOrUpdateHashParameter('statusCode', statusCode);
}

function resetLogs() {
    setMainPageView(`/RF/Home/_LatestLogs`, title(4));
    window.location.hash = `#view=4`;
}


