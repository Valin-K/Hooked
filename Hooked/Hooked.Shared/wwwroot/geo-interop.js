window.hookedGeo = {
    getPosition: function () {
        return new Promise(function (resolve, reject) {
            if (!navigator.geolocation) {
                reject('Geolocation is not supported by this browser.');
                return;
            }
            navigator.geolocation.getCurrentPosition(
                function (pos) {
                    resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude });
                },
                function (err) {
                    reject(err.message);
                },
                { enableHighAccuracy: false, timeout: 10000, maximumAge: 300000 }
            );
        });
    }
};
